#System Management and Infrastructure

##Sections

1. [Introduction](index.html)
2. [Messaging with Actors](messaging-with-actors.html)
3. [Messaging Channels](messaging-channels.html)
4. [Message Construction](message-construction.html)
5. [Message Routing](message-routing.html)
6. [Message Transformation](message-transformation.html)
7. **Message Endpoints**
	- [Messaging Gateway](#Messaging-Gateway)
	- [Messaging Mapper](#Messaging-Mapper)
	- [Transactional Client/Actor](#Transactional Client/Actor)
	- [Polling Consumer](#Polling-Consumer)
	- [Event-Driven Consumer](#Event-Driven-Consumer)
	- [Competing Consumers](#Competing-Consumers)
	- [Message Dispatcher](#Message-Dispatcher)
	- [Selective Consumer](#Selective-Consumer)
	- [Durable Subscriber](#Durable-Subscriber)
	- [Idempotent Receiver](#Idempotent-Receiver)
	- [Service Activator](#Service-Activator)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Messaging Gateway

```fsharp
type AggregateType = AggregateType of cacheActor: IActorRef
type RegisterAggregateId = RegisterAggregateId of id: string
type OrderMessage = 
    | InitializeOrder of amount: decimal
    | ProcessOrder
type CacheMessage = CacheMessage of id: string * actualMessage: obj * sender: IActorRef
type AggregateRef(id, cache) =
    interface ICanTell with
        member this.Tell (message: obj, sender: IActorRef) = cache <! CacheMessage(id, message, sender)

let order (mailbox: Actor<_>) =
    let rec loop amount = actor {
        let! message = mailbox.Receive ()
        match message with
        | InitializeOrder amount ->
            printfn "Initializing Order with %M" amount
            return! loop amount
        | ProcessOrder ->
            printfn "Processing Order is %A" message
            return! loop amount
    }
    loop 0m

let aggregateCache aggregateFunc (mailbox: Actor<_>) =
    let rec loop aggregateIds = actor {
        let! CacheMessage(id, actualMessage, sender) = mailbox.Receive ()
        let child = mailbox.Context.Child id
        let aggregate = if child = (ActorRefs.Nobody :> IActorRef) then spawn mailbox.Context id <| aggregateFunc // reconstitute aggregate state here if pre-existing
                        else child
        aggregate.Tell (actualMessage, sender)
        return! loop aggregateIds
    }
    loop Set.empty

type DomainModel(name) =
    let mutable aggregateTypeRegistry = Map.empty
    let system = System.create "system" <| Configuration.load ()
    member this.AggregateOf (typeName, id) =
        let (AggregateType cacheActor) = aggregateTypeRegistry |> Map.find typeName
        cacheActor <! RegisterAggregateId(id)
        AggregateRef(id, cacheActor)
    member this.RegisterAggregateType (typeName, aggregateFunc) =
        let actorRef = spawn system typeName <| aggregateCache aggregateFunc
        aggregateTypeRegistry <- aggregateTypeRegistry |> Map.add typeName (AggregateType actorRef)
    member this.Shutdown () = system.Shutdown ()

let orderType = "Order"
let model = DomainModel("OrderProcessing")
model.RegisterAggregateType (orderType, order)
let orderRef = model.AggregateOf (orderType, "123")

orderRef <! InitializeOrder 249.95m
orderRef <! ProcessOrder
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/MessagingGateway.fsx" target="_blank">Complete Code</a>

##Messaging Mapper

```fsharp
type QueryMonthlyOrdersFor = QueryMonthlyOrdersFor of customerId: string
type ReallyBigQueryResult = ReallyBigQueryResult of messageBody: string
type IMessageSerializer =
    abstract member Serialize: obj -> string
    abstract member Deserialize: string -> 'a

let serializer = { 
    new IMessageSerializer with
        member this.Serialize obj = JsonConvert.SerializeObject obj
        member this.Deserialize json = JsonConvert.DeserializeObject<'a>(json) 
    }
        
let orderQueryService (serializer: IMessageSerializer) (mailbox: Actor<_>) =
    let monthlyOrdersFor customerId = [ for i in [1 .. 10] -> sprintf "Order data %i" i ]
    let rec loop () = actor {
        let! QueryMonthlyOrdersFor(customerId) as message = mailbox.Receive ()
        printfn "OrderQueryService: Received %A" message
        let queryResult = monthlyOrdersFor customerId
        let messageBody = serializer.Serialize(queryResult)
        mailbox.Sender () <! ReallyBigQueryResult messageBody
        return! loop ()
    }
    loop ()

let caller orderQueryService (mailbox: Actor<_>) =
    orderQueryService <! QueryMonthlyOrdersFor "123"
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Caller: result received: %A" message
        return! loop ()
    }
    loop ()

let orderQueryServiceRef = spawn system "orderQueryService" <| orderQueryService serializer
let callerRef = spawn system "caller" <| caller orderQueryServiceRef
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/MessagingMapper.fsx" target="_blank">Complete Code</a>

##Transactional Client/Actor

```fsharp
// TBD: Akka Persistence is not fully supported yet
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/TransactionalClientActor.fsx" target="_blank">Complete Code</a>

##Polling Consumer

```fsharp
type WorkItem = { Name: string }
type WorkConsumerMessage = 
    | WorkNeeded
    | WorkItemsAllocated of workItems: WorkItem list
    | WorkOnItem of workItem: WorkItem
type WorkItemsProvider = 
    | AllocateWorkItems of numberOfItems: int

let workItemsProvider (mailbox: Actor<_>) =
    let rec loop workItemsNamed = actor {
        let allocateWorkItems numberOfItems =
            let allocatedWorkItems = 
                [ 1 .. numberOfItems ]
                |> List.map (fun itemCount -> 
                    let nameIndex = workItemsNamed + itemCount
                    { Name = "WorkItem" + nameIndex.ToString () })
            allocatedWorkItems, workItemsNamed + numberOfItems

        let! AllocateWorkItems numberOfItems = mailbox.Receive ()
        let allocatedWorkItems, workItemsNamed = allocateWorkItems(numberOfItems)
        mailbox.Sender () <! WorkItemsAllocated allocatedWorkItems
        return! loop workItemsNamed
    }
    loop 0

let workConsumer workItemsProvider (mailbox: Actor<_>) =
    mailbox.Defer (fun () -> mailbox.Context.Stop(workItemsProvider))
    let rec loop totalItemsWorkedOn = actor {
        let performWorkOn workItem =
            let totalItemsWorkedOn = totalItemsWorkedOn + 1
            if (totalItemsWorkedOn >= 15) then  mailbox.Context.Stop mailbox.Self else ()
            totalItemsWorkedOn

        let! message = mailbox.Receive ()
        match message with
        | WorkItemsAllocated workitems ->
            printfn "WorkItemsAllocated..."
            workitems |> List.iter (fun workItem -> mailbox.Self <! WorkOnItem(workItem))
            mailbox.Self <! WorkNeeded
            return! loop totalItemsWorkedOn
        | WorkNeeded ->
            printfn "WoorkNeeded..."
            workItemsProvider <! AllocateWorkItems 5
            return! loop totalItemsWorkedOn
        | WorkOnItem workItem ->
            printfn "Performed work on: %s" workItem.Name
            let totalItemsWorkedOn = performWorkOn workItem
            return! loop totalItemsWorkedOn
    }
    loop 0

let workItemsProviderRef = spawn system "workItemsProvider" workItemsProvider
let workConsumerRef = spawn system "workConsumer" <| workConsumer workItemsProviderRef

workConsumerRef <! WorkNeeded
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/PollingConsumer.fsx" target="_blank">Complete Code</a>

##Event-Driven Consumer

```fsharp
// No code example
```

[Sections](#Sections)

<a href="" target="_blank">Complete Code</a>

##Competing Consumers

```fsharp
type WorkItem = { Name: string }

let workConsumer (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! workItem = mailbox.Receive ()
        printfn "%s for: %s" mailbox.Self.Path.Name workItem.Name
        return! loop ()
    }
    loop ()

let workItemsProviderRef = spawnOpt system "workItemsProvider" workConsumer [ Router(Akka.Routing.SmallestMailboxPool(5)) ]

[ 1 .. 100 ]
|> List.iter (fun itemCount -> workItemsProviderRef <! { Name = "WorkItem" + itemCount.ToString () })

```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/CompetingConsumers.fsx" target="_blank">Complete Code</a>

##Message Dispatcher

```fsharp
type WorkItem = { Name: string }

let workConsumer (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! workItem = mailbox.Receive ()
        printfn "%s for: %s" mailbox.Self.Path.Name workItem.Name
        return! loop ()
    }
    loop ()

let workItemsProvider = spawnOpt system "workItemsProvider" workConsumer [ Router(Akka.Routing.RoundRobinPool(5)) ]

workItemsProvider <! { Name = "WorkItem1" }
workItemsProvider <! { Name = "WorkItem2" }
workItemsProvider <! { Name = "WorkItem3" }
workItemsProvider <! { Name = "WorkItem4" }
workItemsProvider <! { Name = "WorkItem5" }
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/MessageDispatcher.fsx" target="_blank">Complete Code</a>

##Selective Consumer

```fsharp
type MessageTypeA = MessageTypeA
type MessageTypeB = MessageTypeB
type MessageTypeC = MessageTypeC

let selectiveConsumer (consumerOfA: IActorRef) (consumerOfB: IActorRef) (consumerOfC: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? MessageTypeA -> 
            consumerOfA.Forward message
            return! loop ()
        | :? MessageTypeB -> 
            consumerOfB.Forward message
            return! loop ()
        | :? MessageTypeC -> 
            consumerOfC.Forward message
            return! loop ()
        | _ -> return! loop ()
    }
    loop ()

let consumerOfMessageTypeA (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "ConsumerOfMessageTypeA: %A" message
        return! loop ()
    }
    loop ()

let consumerOfMessageTypeB (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "ConsumerOfMessageTypeB: %A" message
        return! loop ()
    }
    loop ()

let consumerOfMessageTypeC (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "ConsumerOfMessageTypeC: %A" message
        return! loop ()
    }
    loop ()

let consumerOfARef = spawn system "consumerOfA" consumerOfMessageTypeA
let consumerOfBRef = spawn system "consumerOfB" consumerOfMessageTypeB
let consumerOfCRef = spawn system "consumerOfC" consumerOfMessageTypeC
let selectiveConsumerRef = spawn system "selectiveConsumer" <| selectiveConsumer consumerOfARef consumerOfBRef consumerOfCRef

selectiveConsumerRef <! MessageTypeA
selectiveConsumerRef <! MessageTypeB
selectiveConsumerRef <! MessageTypeC
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/SelectiveConsumer.fsx" target="_blank">Complete Code</a>

##Durable Subscriber

```fsharp
// No code example
```

[Sections](#Sections)

<a href="" target="_blank">Complete Code</a>

##Idempotent Receiver

```fsharp
// TBD: Akka Persistence is not fully supported yet
```

[Sections](#Sections)

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/DurableSubscriber.fsx" target="_blank">Complete Code</a>

##Service Activator

```fsharp
// No code example
```

[Sections](#Sections)
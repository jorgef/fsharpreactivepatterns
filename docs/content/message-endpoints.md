#Message Endpoints

For more details and full analysis of each pattern described in this section, please refer to **Chapter 9** of <a href="http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">Reactive Messaging Patterns with the Actor Model</a> by <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a>.

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

The Messaging Gateway pattern encapsulates access to the messaging system.

```fsharp
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

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/MessagingGateway.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Messaging Mapper

This pattern domain types to messages.

```fsharp
type QueryMonthlyOrdersFor = QueryMonthlyOrdersFor of customerId: string
type ReallyBigQueryResult = ReallyBigQueryResult of messageBody: string
        
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

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/MessagingMapper.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Transactional Client/Actor

This pattern adds transactional behavior between senders and receivers.

```fsharp
// TBD: Akka Persistence is not fully supported yet
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/TransactionalClientActor.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Polling Consumer

This pattern enables the actor to have control of when the message is consumed.

```fsharp
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

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/PollingConsumer.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Event-Driven Consumer

This pattern is implemented by Akka.NET as actors are event-driven by design.

```fsharp
// No code example
```

[Sections](#Sections)

##Competing Consumers

This pattern allows to distribute the work between multiple consumers.

```fsharp
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

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/CompetingConsumers.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Dispatcher

The Message Dispatcher pattern distributes messages across multiple actors in high workload scenarios.

```fsharp
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

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/MessageDispatcher.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Selective Consumer

This pattern enables the selection/filtering of message types that will be consumed. 

```fsharp
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

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/SelectiveConsumer.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Durable Subscriber

The Durable Subscriber pattern ensures that the consumer won't miss messages. 

```fsharp
// TBD: Akka Persistence is not fully supported yet
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/DurableSubscriber.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Idempotent Receiver

This pattern allows the consumer to safely receive the same messages multiple times.

### Deduplication
```fsharp
type Transaction = Transaction of transactionId: TransactionId * amount: Money
type AccountBalance = AccountBalance of accountId: AccountId * amount: Money
type AccountMessage = 
    | Deposit of transactionId: TransactionId * amount: Money
    | Withdraw of transactionId: TransactionId * amount: Money
    | QueryBalance

let account accountId (mailbox: Actor<_>) =
    let rec loop transactions = actor {
        let calculateBalance () = 
            let amount = 
                transactions 
                |> Map.toList
                |> List.sumBy (fun (_,Transaction(_, Money amount)) -> amount)
            printfn "Balance: %M" amount
            AccountBalance(accountId, Money amount)

        let! message = mailbox.Receive ()
        match message with
        | Deposit(transactionId, amount) -> 
            let transaction = Transaction(transactionId, amount)
            printfn "Deposit: %A" transaction
            return! loop (transactions |> Map.add transactionId transaction)
        | Withdraw(transactionId, Money amount) -> 
            let transaction = Transaction(transactionId, Money -amount)
            printfn "Withdraw: %A" transaction
            return! loop (transactions |> Map.add transactionId transaction)
        | QueryBalance -> 
            mailbox.Sender () <! calculateBalance () // this msg is sent to the deadletter
            return! loop transactions
    }
    loop Map.empty

let accountRef = spawn system "account" <| account (AccountId "acc1")
let deposit1 = Deposit(TransactionId "tx1", Money(100m))

accountRef <! deposit1
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Deposit(TransactionId "tx2", Money(20m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Withdraw(TransactionId "tx3", Money(50m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Deposit(TransactionId "tx4", Money(70m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Withdraw(TransactionId "tx5", Money(100m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Deposit(TransactionId "tx6", Money(10m))
accountRef <! QueryBalance
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/IdempotentReceiverDeduplication.fsx" target="_blank">Complete Code (Deduplication)</a>

### Become
```fsharp
type Document = { Text: string option } with
    member this.DetermineClassification () =
        this.Text
        |> Option.fold (fun _ text ->
            match text.ToLower () with
            | text when text.Contains "low" -> "Low"
            | text when text.Contains "medium" -> "Medium"
            | text when text.Contains "high" -> "High"
            | _  -> "Unknown")
            "Unknown"
    member this.IsNotAttached with get () = this.Text |> Option.fold (fun _ text -> String.IsNullOrEmpty text) false 
    member this.IsDefined with get () = this.Text.IsSome

let riskAssessment (mailbox: Actor<_>) =
    let rec documented (document: Document) = actor {
        let! message = mailbox.Receive ()
        match message with
        | AttachDocument _ ->
            // already received; ignore
            return! documented document
        | ClassifyRisk ->
            mailbox.Sender () <! RiskClassified(document.DetermineClassification ())
            return! documented document
    }
    let rec undocumented (document: Document) = actor {
        let! message = mailbox.Receive ()
        match message with
        | AttachDocument documentText ->
            let document = { Text = Some documentText }
            return! documented document
        | ClassifyRisk ->
            mailbox.Sender () <! RiskClassified("Unknown")
            return! undocumented document
    }
    undocumented { Text = None }

let riskAssessmentRef = spawn system "riskAssessment" riskAssessment

let futureAssessment1: RiskClassified = riskAssessmentRef <? ClassifyRisk |> Async.RunSynchronously
printfn "%A" futureAssessment1
riskAssessmentRef <! AttachDocument("This is a HIGH risk.")
let futureAssessment2: RiskClassified = riskAssessmentRef <? ClassifyRisk |> Async.RunSynchronously
printfn "%A" futureAssessment2
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageEndpoints/IdempotentReceiverBecome.fsx" target="_blank">Complete Code (Become)</a>

[Sections](#Sections)

##Service Activator

The Service Activator pattern provides a way to invoke an internal application service when a message arrives.

```fsharp
// No code example
```

[Sections](#Sections)
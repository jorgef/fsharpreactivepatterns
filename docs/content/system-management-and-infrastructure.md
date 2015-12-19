#System Management and Infrastructure

For more details and full analysis of each pattern described in this section, please refer to **Chapter 10** of <a href="http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">Reactive Messaging Patterns with the Actor Model</a> by <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a>.

##Sections

1. [Introduction](index.html)
2. [Messaging with Actors](messaging-with-actors.html)
3. [Messaging Channels](messaging-channels.html)
4. [Message Construction](message-construction.html)
5. [Message Routing](message-routing.html)
6. [Message Transformation](message-transformation.html)
7. [Message Endpoints](message-endpoints.html)
8. **System Management and Infrastructure**
	- [Control Bus](#Control-Bus)
	- [Detour](#Detour)
	- [Wire Tap](#Wire-Tap)
	- [Message Metadata/History](#Message-Metadata-History)
	- [Message Journal/Store](#Message-Journal-Store)
	- [Smart Proxy](#Smart-Proxy)
	- [Test Message](#Test-Message)
	- [Channel Purger](#Channel-Purger)

##Control Bus

The Control Bus pattern allows you to administer and control actors and messages.

```fsharp
// No code example
```

[Sections](#Sections)

##Detour

This pattern enables re-routing messages to execute additional steps.

```fsharp
let orderProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "OrderProcessor: %A" message
        return! loop ()
    }
    loop ()

let messageDebugger next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageDebugger: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let messageTester next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageTester: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let messageValidator next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageValidator: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let messageLogger next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageLogger: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let orderProcessorRef = spawn system "orderProcessor" orderProcessor
let debuggerRef = spawn system "debugger" <| messageDebugger orderProcessorRef
let testerRef = spawn system "tester" <| messageTester debuggerRef
let validatorRef = spawn system "validator" <| messageValidator testerRef
let loggerRef = spawn system "logger" <| messageLogger validatorRef

let orderProcessorDetour = loggerRef // change to = orderProcessorRef to turn the Detour off

orderProcessorDetour <! ProcessOrder(Order)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/SystemManagementInfrastructure/Detour.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Wire Tap

The Wire Tap pattern allows to inspect messages without altering its content and destination.

```fsharp
let orderProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "OrderProcessor: %A" message
        return! loop ()
    }
    loop ()

let messageLogger next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageLogger: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let orderProcessorRef = spawn system "orderProcessor" orderProcessor
let loggerRef = spawn system "logger" <| messageLogger orderProcessorRef

let orderProcessorWireTap = loggerRef

orderProcessorWireTap <! ProcessOrder(Order)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/SystemManagementInfrastructure/WireTap.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Metadata/History

This pattern adds metadata and history to messages to provide tracking information.

```fsharp
type Entry = { Who: Who; What: What; Where: Where; When: DateTimeOffset; Why: Why } with
    static member Create (who, what, where, ``when``, why) = { Who = who; What = what; Where = where; When = ``when``; Why = why }
    static member Create (who: Who, what: What, where: Where, why: Why) = Entry.Create (who, what, where, DateTimeOffset.UtcNow, why)
    static member Create (who, what, actorType, actorName, why) =  Entry.Create (Who who, What what, Where(actorType, actorName), Why why)
    member this.AsMetadata () = (Metadata.Create () : Metadata).Including this
and Metadata = { Entries: Entry list } with
    static member Create () = { Entries = [] }
    member this.Including entry = { Entries = this.Entries @ [entry] }
type SomeMessage = SomeMessage of payload: string * metadata: Metadata with
    static member Create payload = SomeMessage(payload, Metadata.Create ())
    member this.Including entry =
        let (SomeMessage(payload, metadata)) = this 
        SomeMessage(payload, metadata.Including entry)

let processor next (mailbox: Actor<SomeMessage>) =
    let random = Random()
    let user = sprintf "user%i" (random.Next 100)
    let wasProcessed = sprintf "Processed: %i" (random.Next 5)
    let because = sprintf "Because: %i" (random.Next 10)
    let entry = Entry.Create (Who user, What wasProcessed, Where("processor", mailbox.Self.Path.Name), DateTimeOffset.UtcNow, Why because)
    let report message heading = printfn "%s %s: %A" mailbox.Self.Path.Name (defaultArg heading "received") message
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        report message None
        let nextMessage = message.Including entry
        match next with
        | Some next -> next <! nextMessage
        | None -> report nextMessage <| Some "complete"
        return! loop ()
    }
    loop ()

let processor3Ref = spawn system "processor3" <| processor None
let processor2Ref = spawn system "processor2" <| processor (Some processor3Ref)
let processor1Ref = spawn system "processor1" <| processor (Some processor2Ref)

let entry = Entry.Create (Who "driver", What "Started", Where("processor", "driver"), DateTimeOffset.UtcNow, Why "Running processors")
processor1Ref <! SomeMessage("Data...", entry.AsMetadata ())
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/SystemManagementInfrastructure/MessageMetadataHistory.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Journal/Store

This pattern records the delivered messages in a permanent location.

```fsharp
// No code example
```

[Sections](#Sections)

##Smart Proxy

The Smart Proxy pattern allows to track messages that are using the Return Address pattern.

```fsharp
type ServiceRequest =
    | ServiceRequestOne of requestId : string
    | ServiceRequestTwo of requestId : string
    | ServiceRequestThree of requestId : string with
    member this.RequestId with get () = match this with | ServiceRequestOne requestId | ServiceRequestTwo requestId | ServiceRequestThree requestId -> requestId
type ServiceReply = 
    | ServiceReplyOne of replyId : string
    | ServiceReplyTwo of replyId : string
    | ServiceReplyThree of replyId : string with 
    member this.ReplyId with get () = match this with | ServiceReplyOne replyId | ServiceReplyTwo replyId | ServiceReplyThree replyId -> replyId 
type RequestService = RequestService of service: ServiceRequest

let serviceProvider (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ServiceRequestOne requestId -> mailbox.Sender () <! ServiceReplyOne requestId
        | ServiceRequestTwo requestId -> mailbox.Sender () <! ServiceReplyTwo requestId
        | ServiceRequestThree requestId -> mailbox.Sender () <! ServiceReplyThree requestId
        return! loop ()
    }
    loop ()

let serviceRequester serviceProvider (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? RequestService as request -> 
            printfn "ServiceRequester: %s: %A" mailbox.Self.Path.Name request
            let (RequestService service) = request
            serviceProvider <! service
        | reply -> printfn "ServiceRequester: %s: %A" mailbox.Self.Path.Name reply
        return! loop ()
    }
    loop ()

let serviceProviderProxy serviceProvider (mailbox: Actor<_>) =
    let analyzeReply reply = printfn "Reply analyzed: %A" reply
    let analyzeRequest request = printfn "Request analyzed: %A" request

    let rec loop requesters = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? ServiceRequest as request ->
            let requesters = requesters |> Map.add request.RequestId (mailbox.Sender ())
            serviceProvider <! request
            analyzeRequest request
            return! loop requesters
        | :? ServiceReply as reply -> 
            let requester = requesters |> Map.tryFind reply.ReplyId
            match requester with 
            | Some sender ->
                analyzeReply reply
                sender <! reply
                let requesters = requesters |> Map.remove reply.ReplyId
                return! loop requesters
            | None -> return! loop requesters
        | _ -> return! loop requesters
    }
    loop Map.empty

let serviceProviderRef = spawn system "serviceProvider" serviceProvider
let proxyRef = spawn system "proxy" <| serviceProviderProxy serviceProviderRef
let requester1Ref = spawn system "requester1" <| serviceRequester proxyRef
let requester2Ref = spawn system "requester2" <| serviceRequester proxyRef
let requester3Ref = spawn system "requester3" <| serviceRequester proxyRef

requester1Ref <! RequestService(ServiceRequestOne "1")
requester2Ref <! RequestService(ServiceRequestTwo "2")
requester3Ref <! RequestService(ServiceRequestThree "3")
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/SystemManagementInfrastructure/SmartProxy.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Test Message

This pattern allows to check the health of an actor.

```fsharp
type Message = { IsTest: bool }

let inline (|TestMessage|_|) (message: 'a) = 
    let inline isTest (message: ^a) = (^a: (member get_IsTest: unit -> bool) (message))
    if (isTest message) then Some message else None

let processor (mailbox: Actor<Message>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | TestMessage message -> printfn "Test message: %A" message
        | message -> printfn "Production message: %A" message
        return! loop ()
    }
    loop ()

let processorRef = spawn system "processor" processor

processorRef <! { IsTest = true }
processorRef <! { IsTest = false }
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/SystemManagementInfrastructure/TestMessage.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Channel Purger

The Channel Purger pattern removes messages from an actor or Message Store.

```fsharp
let orderProcessor (mailbox: Actor<_>) =
    let rec normal () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessOrder -> 
            printfn "Normal: %A" message
            return! normal ()
        | PurgeNow -> 
            printfn "Normal: %A" message
            return! purger ()
        | _ -> return! normal () }
    and purger () = actor {
        let! message = mailbox.Receive ()
        match message with
        | StopPurge -> 
            printfn "Purger: %A" message
            return! normal ()
        | _ -> return! purger ()
    }
    normal ()

let orderProcessorRef = spawn system "orderProcessor" orderProcessor

orderProcessorRef <! ProcessOrder
orderProcessorRef <! PurgeNow
orderProcessorRef <! StopPurge
orderProcessorRef <! ProcessOrder
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/SystemManagementInfrastructure/ChannelPurger.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)
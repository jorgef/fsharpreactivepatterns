#Messaging Channels

For more details and full analysis of each pattern described in this section, please refer to **Chapter 5** of <a href="http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">Reactive Messaging Patterns with the Actor Model</a> by <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a>.


##Sections

1. [Introduction](index.html)
2. [Messaging with Actors](messaging-with-actors.html)
3. **Messaging Channels**
	- [Point-to-Point Channel](#Point-to-Point-Channel)
	- [Publish-Subscribe Channel](#Publish-Subscribe-Channel)
	- [Datatype Channel](#Datatype-Channel)
	- [Invalid Message Channel](#Invalid-Message-Channel)
	- [Dead Letter Channel](#Dead-Letter-Channel)
	- [Guaranteed Delivery](#Guaranteed-Delivery)
	- [Channel Adapter](#Channel-Adapter)
	- [Message Bridge](#Message-Bridge)
	- [Message Bus](#Message-Bus)
4. [Message Construction](message-construction.html)
5. [Message Routing](message-routing.html)
6. [Message Transformation](message-transformation.html)
7. [Message Endpoints](message-endpoints.html)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Point-to-Point Channel

When an actor sends a message to another specific actor, it is using a Point-to-Point Channel.

```fsharp
let actorA actorB (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    actorB <! "Hello, from actor A!"
    actorB <! "Hello again, from actor A!"
    loop ()

let actorB (mailbox: Actor<string>) =
    let rec loop hello helloAgain goodbye goodbyeAgain = actor {
        let! message = mailbox.Receive ()
        let hello = hello + (if message.Contains "Hello" then 1 else 0)
        let helloAgain = helloAgain + (if message.Contains "Hello again" then 1 else 0)
        assertion (hello = 0 || hello > helloAgain)
        let goodbye = goodbye + (if message.Contains "Goodbye" then 1 else 0)
        let goodbyeAgain = goodbyeAgain + (if message.Contains "Goodbye again" then 1 else 0)
        assertion (goodbye = 0 || goodbye > goodbyeAgain)
        printfn "ActorB: received %s" message
        return! loop hello helloAgain goodbye goodbyeAgain
    }
    loop 0 0 0 0 

let actorC actorB (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    actorB <! "Goodbye, from actor C!"
    actorB <! "Goodbye again, from actor C!"
    loop ()

let actorBRef = spawn system "actorB" actorB
let actorARef = spawn system "actorA" <| actorA actorBRef
let actorCRef = spawn system "actorC" <| actorC actorBRef
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/PointToPointChannel.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Publish-Subscribe Channel

This pattern allows actors to publish messages that can be delivered to multiple (subscribed) actors.

```fsharp
let quoteListener (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! { Market = Market m; Ticker = Symbol t; Price = Money p } = mailbox.Receive ()
        printfn "QuoteListener: PricedQuoted received, market: %s, ticker: %s, price: %M" m t p 
        return! loop ()
    }
    loop () 

let quoteListenerRef = spawn system "quoteListenerRef" quoteListener
subscribe typeof<PricedQuoted> quoteListenerRef system.EventStream
publish { Market = Market("quotes/NASDAQ"); Ticker = Symbol "MSFT"; Price = Money(37.16m) } system.EventStream
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/PublishSubscribeChannel.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Datatype Channel

When actors receive typed messages, they are using a Datatype Channel. In cases when they receive serialized messages, you can use specific actors to create a typed message.

```fsharp
let productQueriesChannel (mailbox: Actor<_>) =
    let translateToProductQuery message = message |> Encoding.UTF8.GetString |> ProductQuery
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let (ProductQuery value) = message |> translateToProductQuery
        printfn "ProductQueriesChannel: ProductQuery received, value: %s" <| value
        return! loop ()
    }
    loop ()

let productQueriesChannelRef = spawn system "productQueriesChannel" productQueriesChannel

productQueriesChannelRef <! Encoding.UTF8.GetBytes "test query"
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/DatatypeChannel.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Invalid Message Channel

This pattern deals with not supported messages received by an actor.


```fsharp
type InvalidMessage<'a> = { Sender: IActorRef; Receiver: IActorRef; Message: 'a }

let invalidMessageChannel (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! { Sender = s; Receiver = r; Message = m } = mailbox.Receive ()
        printfn "InvalidMessageChannel: InvalidMessage received, message: %A" m
        return! loop ()
    }
    loop ()

let authenticator (nextFilter: IActorRef) (invalidMessageChannel: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? ProcessIncomingOrder as message ->
            let (ProcessIncomingOrder(bytes)) = message 
            let text = Encoding.Default.GetString bytes
            printfn "Decrypter: processing %s" text
            let orderText = text.Replace ("(encryption)", String.Empty)
            nextFilter <! ProcessIncomingOrder(Encoding.Default.GetBytes orderText)
        | invalid -> invalidMessageChannel <! { Sender = mailbox.Sender (); Receiver = mailbox.Self; Message = invalid }
        return! loop () 
    }
    loop ()

let nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    loop ()

let invalidMessageChannelRef = spawn system "invalidMessageChannel" invalidMessageChannel
let nextFilterRef = spawn system "nextFilter" nextFilter
let authenticatorRef = spawn system "authenticator" <| authenticator nextFilterRef invalidMessageChannelRef

authenticatorRef <! "Invalid message"
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/InvalidMessageChannel.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)


##Dead Letter Channel

The Dead Letter Channel receives all the messages that didn't reach the destination. Any actor can subscribe to the Dead Letter Channel implemented by Akka.NET.

```fsharp
let sysListener (mailbox: Actor<DeadLetter>) = 
    let rec loop () = actor {
        let! deadLetter = mailbox.Receive ()
        printfn "SysListner, DeadLetter received: %A" deadLetter.Message
        return! loop ()
    }
    loop ()

let sysListenerRef = spawn system "sysListener" sysListener
subscribe typeof<DeadLetter> sysListenerRef system.EventStream

let deadActorRef = select "akka://system/user/deadActor" system
deadActorRef <! "Message to dead actor"
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/DeadLetterChannel.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)


##Guaranteed Delivery

This pattern ensures that all the messages are received by the destination actor. 

```fsharp
// TBD: Akka Persistence is not fully supported yet
```

[Sections](#Sections)


##Channel Adapter

This pattern is used to expose a messaging interface and redirect the messages to application.

```fsharp
let stockTrader (tradingBus: IActorRef) (buyerService: BuyerService) (sellerService: SellerService) (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteBuyOrder", mailbox.Self)
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteSellOrder", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(i, s, q, p) -> 
            let result = buyerService.PlaceBuyOrder (i, s, q, p)
            tradingBus <! TradingNotification("BuyOrderExecuted", BuyOrderExecuted(result.PortfolioId, result.OrderId, result.Symbol, result.Quantity, result.TotalCost))
        | ExecuteSellOrder(i, s, q, p) -> 
            let result = buyerService.PlaceBuyOrder (i, s, q, p)
            tradingBus <! TradingNotification("SellOrderExecuted", SellOrderExecuted(result.PortfolioId, result.OrderId, result.Symbol, result.Quantity, result.TotalCost))
        return! loop ()
    }
    loop ()

let tradingBus (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TradingBus: received %A" message
        return! loop ()
    }
    loop ()

let tradingBusRef = spawn system "tradingBus" tradingBus
let stockTraderRef = spawn system "stockTrader" (stockTrader <| tradingBusRef <| new BuyerService() <| new SellerService ())

stockTraderRef <! ExecuteBuyOrder("1", Symbol "S1", 5, Money 10m)
stockTraderRef <! ExecuteSellOrder("2", Symbol "S2", 3, Money 8m)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/ChannelAdapter.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)


##Message Bridge

A Message Bridge is used to integrate two applications that use different messaging technologies.

```fsharp
type RabbitMQTextMessage = RabbitMQTextMessage of string

let inventoryProductAllocationBridge (mailbox: Actor<_>) =
    let translatedToInventoryProductAlloction = sprintf "Inventory product alloction for %s"
    let acknowledgeDelivery (RabbitMQTextMessage textMessage) = printfn "InventoryProductAllocationBridge: acknowledged '%s'" textMessage

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let (RabbitMQTextMessage textMessage) = message
        printfn "InventoryProductAllocationBridge: received '%s'" textMessage
        let inventoryProductAllocation = translatedToInventoryProductAlloction textMessage
        printfn "InventoryProductAllocationBridge: translated '%s'" inventoryProductAllocation
        acknowledgeDelivery message
        return! loop ()
    }
    loop ()

let inventoryProductAllocationBridgeRef = spawn system "inventoryProductAllocationBridge" inventoryProductAllocationBridge

inventoryProductAllocationBridgeRef <! RabbitMQTextMessage "Rabbit test message"
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/MessageBridge.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)


##Message Bus

The Message Bus is used to communicate different applications that need to work together.

```fsharp
let tradingBus (mailbox: Actor<_>) =
    let rec loop commandHandlers notificationInterests = actor {
        let dispatchCommand commandId command =
            commandHandlers 
            |> Map.tryFind commandId
            |> Option.map (fun hs -> hs |> List.iter (fun (CommandHandler(_, h)) -> h <! command))
            |> ignore

        let dispatchNotification notificationId notification =
            notificationInterests 
            |> Map.tryFind notificationId
            |> Option.map (fun hs -> hs |> List.iter (fun (NotificationInterest(_, i)) -> i <! notification))
            |> ignore

        let registerCommandHandler commandId applicationId handler =
            let commandHandler = CommandHandler(applicationId, handler)
            commandHandlers
            |> Map.tryFind commandId
            |> Option.fold (fun _ hs -> commandHandler :: hs) [commandHandler]
            |> fun hs -> Map.add commandId hs commandHandlers

        let registerNotificationInterest notificationId applicationId interested =
            let notificationInterest = NotificationInterest(applicationId, interested)
            notificationInterests
            |> Map.tryFind notificationId
            |> Option.fold (fun _ is -> notificationInterest :: is) [notificationInterest]
            |> fun is -> Map.add notificationId is notificationInterests

        let! message = mailbox.Receive ()
        match message with
        | RegisterCommandHandler(applicationId, commandId, handler) -> 
            return! loop (registerCommandHandler commandId applicationId handler) notificationInterests
        | RegisterNotificationInterest(applicationId, notificationId, interested) -> 
            return! loop commandHandlers (registerNotificationInterest notificationId applicationId interested)
        | TradingCommand(commandId, command) -> dispatchCommand commandId command
        | TradingNotification(notificationId, notification) -> dispatchNotification notificationId notification
        | Status -> 
            printfn "TradingBus: STATUS: %A" commandHandlers
            printfn "TradingBus: STATUS: %A" notificationInterests
        return! loop commandHandlers notificationInterests
    }
    loop Map.empty Map.empty

let marketAnalysisTools tradingBus (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterNotificationInterest(applicationId, "BuyOrderExecuted", mailbox.Self)
    tradingBus <! RegisterNotificationInterest(applicationId, "SellOrderExecuted", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | BuyOrderExecuted _ as executed -> printfn "MarketAnalysisTools: adding holding: %A" executed
        | SellOrderExecuted _ as executed -> printfn "MarketAnalysisTools: adjusting holding: %A" executed
        return! loop () 
    }
    loop ()

let portfolioManager tradingBus (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterNotificationInterest(applicationId, "BuyOrderExecuted", mailbox.Self)
    tradingBus <! RegisterNotificationInterest(applicationId, "SellOrderExecuted", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | BuyOrderExecuted _ as executed -> printfn "PortfolioManager: adding holding: %A" executed
        | SellOrderExecuted _ as executed -> printfn "PortfolioManager: adjusting holding: %A" executed
        return! loop () 
    }
    loop ()

let stockTrader tradingBus (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteBuyOrder", mailbox.Self)
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteSellOrder", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(portfolioId, symbol, quantity, price) as buy -> 
            printfn "StockTrader: buying for: %A" buy
            tradingBus <! TradingNotification("BuyOrderExecuted", BuyOrderExecuted(portfolioId, symbol, quantity, price))
        | ExecuteSellOrder(portfolioId, symbol, quantity, price) as sell ->
            printfn "StockTrader: selling for: %A" sell
            tradingBus <! TradingNotification("BuyOrderExecuted", SellOrderExecuted(portfolioId, symbol, quantity, price))
        return! loop () 
    }
    loop ()

let tradingBusRef = spawn system "tradingBus" tradingBus
let marketAnalysisToolsRef = spawn system "marketAnalysisTools" <| marketAnalysisTools tradingBusRef
let portfolioManagerRef = spawn system "portfolioManager" <| portfolioManager tradingBusRef
let stockTraderRef = spawn system "stockTrader" <| stockTrader tradingBusRef

tradingBusRef <! Status
tradingBusRef <! TradingCommand("ExecuteBuyOrder", ExecuteBuyOrder("p123", "MSFT", 100, Money 31.85m))
tradingBusRef <! TradingCommand("ExecuteSellOrder", ExecuteSellOrder("p456", "MSFT", 200, Money 31.80m))
tradingBusRef <! TradingCommand("ExecuteBuyOrder", ExecuteBuyOrder("p789", "MSFT", 100, Money 31.83m))
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingChannels/MessageBus.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)
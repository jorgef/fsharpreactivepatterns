#Message Construction

For more details and full analysis of the patterns described in this section, please refer to the **Chapter 6** of the book <a href="http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">Reactive Messaging Patterns with the Actor Model</a> by <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a>.


##Sections

1. [Introduction](index.html)
2. [Messaging with Actors](messaging-with-actors.html)
3. [Messaging Channels](messaging-channels.html)
4. **Message Construction**
	- [Command Message](#Command-Message)
	- [Document Message](#Document-Message)
	- [Event Message](#Event-Message)
	- [Request-Reply](#Request-Reply)
	- [Return Address](#Return-Address)
	- [Correlation Identifier](#Correlation-Identifier)
	- [Message Sequence](#Message-Sequence)
	- [Message Expiration](#Message-Expiration)
	- [Format Indicator](#Format-Indicator)
5. [Message Routing](message-routing.html)
6. [Message Transformation](message-transformation.html)
7. [Message Endpoints](message-endpoints.html)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Command Message

```fsharp
type TradingCommand =
    | ExecuteBuyOrder of portfolioId: string * symbol: string * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: string * quantity: int * price: Money

let stockTrader (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(portfolioId, symbol, quantity, price) as buy -> 
            printfn "StockTrader: buying for: %A" buy
        | ExecuteSellOrder(portfolioId, symbol, quantity, price) as sell ->
            printfn "StockTrader: selling for: %A" sell
        return! loop () 
    }
    loop ()

let stockTraderRef = spawn system "stockTrader" <| stockTrader

stockTraderRef <! ExecuteBuyOrder("p123", "MSFT", 100, Money 31.85m)
stockTraderRef <! ExecuteSellOrder("p456", "MSFT", 200, Money 31.80m)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/CommandMessage.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Document Message

```fsharp
type PriceQuote = { QuoterId: string; RetailerId: string; RfqId: string; ItemId: string; RetailPrice: decimal; DiscountPrice: decimal }
type QuotationFulfillment = { RfqId: string; QuotesRequested: int; PriceQuotes: PriceQuote seq; Requester: IActorRef }
type RequestPriceQuote = RequestPriceQuote of rfqId: string * itemId: string * retailPrice: Money * orderTotalRetailPrice: Money

let quotation (mailbox: Actor<_>) =
    let quoterId = mailbox.Self.Path.Name
    let rec loop () = actor {
        let! RequestPriceQuote(rfqId, itemId, Money retailPrice, _) = mailbox.Receive ()
        mailbox.Sender () <! { RfqId = rfqId; QuotesRequested = 1; PriceQuotes = [{ QuoterId = quoterId; RetailerId = "Retailer1"; RfqId = rfqId; ItemId = itemId; RetailPrice = retailPrice; DiscountPrice = retailPrice * 0.90m }]; Requester = mailbox.Sender () }
        return! loop ()
    }
    loop ()

let requester quotation (mailbox: Actor<_>) =
    quotation <! RequestPriceQuote("1", "1", Money 10m, Money 10m)
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Requester: quote %A" message
        return! loop ()
    }
    loop ()

let quotationRef = spawn system "quotation" quotation
let requesterRef = spawn system "requester" <| requester quotationRef
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/DocumentMessage.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Event Message

```fsharp
type PriceQuote = { QuoterId: string; RetailerId: string; RfqId: string; ItemId: string; RetailPrice: decimal; DiscountPrice: decimal }
type RequestPriceQuote = RequestPriceQuote of rfqId: string * itemId: string * retailPrice: Money * orderTotalRetailPrice: Money
type PriceQuoteFulfilled = PriceQuoteFulfilled of priceQuote: PriceQuote 

let quotation subscriber (mailbox: Actor<_>) =
    let quoterId = mailbox.Self.Path.Name
    let rec loop () = actor {
        let! RequestPriceQuote(rfqId, itemId, Money retailPrice, _) = mailbox.Receive ()
        subscriber <! PriceQuoteFulfilled { QuoterId = quoterId; RetailerId = "Retailer1"; RfqId = rfqId; ItemId = itemId; RetailPrice = retailPrice; DiscountPrice = retailPrice * 0.90m }
        return! loop ()
    }
    loop ()

let subscriber (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Requester: event %A" message
        return! loop ()
    }
    loop ()

let subscriberRef = spawn system "subscriber" subscriber
let quotationRef = spawn system "quotation" <| quotation subscriberRef

quotationRef <! RequestPriceQuote("1", "1", Money 10m, Money 10m)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/EventMessage.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Request-Reply

```fsharp
type ServerMessage = Request of string
type ClientMessage =
    | Reply of string
    | StartWith of IActorRef

let client (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | StartWith server ->
            printfn "Client: is starting..."
            server <! Request "REQ-1"
        | Reply what -> printfn "Client: received response: %s" what
        return! loop ()
    }
    loop ()

let server (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! Request what = mailbox.Receive ()
        printfn "Server: received request value: %s" what
        mailbox.Sender () <! Reply (sprintf "RESP-1 for %s" what)
        return! loop ()
    }
    loop ()

let clientRef = spawn system "client" client
let serverRef = spawn system "server" server

clientRef <! StartWith serverRef
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/RequestReply.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Return Address

```fsharp
type ServerMessage = 
    | Request of string
    | RequestComplex of string
type ClientMessage =
    | Reply of string
    | StartWith of IActorRef
    | ReplyToComplex of string
type WorkerMessage =
    | WorkerRequestComplex of string

let client (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | StartWith server ->
            printfn "Client: is starting..."
            server <! Request "REQ-1"
            server <! RequestComplex "REQ-20"
        | Reply what -> printfn "Client: received response: %s" what
        | ReplyToComplex what -> printfn "Client: received reply to complex: %s" what
        return! loop ()
    }
    loop ()

let worker (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! WorkerRequestComplex what = mailbox.Receive ()
        printfn "Worker: received complex request value: %s" what
        mailbox.Sender () <! ReplyToComplex (sprintf "RESP-2000 for %s" what)
        return! loop ()
    }
    loop ()

let server (mailbox: Actor<_>) =
    let workerRef = spawn mailbox.Context "worker" worker
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | Request what -> 
            printfn "Server: received request value: %s" what
            mailbox.Sender () <! Reply (sprintf "RESP-1 for %s" what)
        | RequestComplex what -> 
            printfn "Server: received request value: %s" what
            mailbox.Sender () <! Reply (sprintf "RESP-1 for %s" what)
            workerRef.Forward <| WorkerRequestComplex what
        return! loop ()
    }
    loop ()

let clientRef = spawn system "client" client
let serverRef = spawn system "server" server

clientRef <! StartWith serverRef
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/ReturnAddress.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Correlation Identifier

```fsharp
type PriceQuote = { QuoterId: string; RetailerId: string; RfqId: string; ItemId: string; RetailPrice: decimal; DiscountPrice: decimal }
type RequestPriceQuote = RequestPriceQuote of rfqId: string * itemId: string * retailPrice: decimal * orderTotalRetailPrice: decimal
type PriceQuoteTimedOut = PriceQuoteTimedOut of rfqId: string
type RequiredPriceQuotesForFulfillment = RequiredPriceQuotesForFulfillment of rfqId: string * quotesRequested: int
type QuotationFulfillment = QuotationFulfillment of rfqId: string * quotesRequested: int * priceQuotes: PriceQuote seq * requester: IActorRef
type BestPriceQuotation = BestPriceQuotation of rfqId: string * priceQuotes: PriceQuote seq
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/CorrelationIdentifier.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Sequence

```fsharp
// No code example
```

[Sections](#Sections)

##Message Expiration

```fsharp
type PlaceOrder = { Id: string; OccurredOn: int64; TimeToLive: int64; ItemId: string; Price: Money } with
    static member Create (itemId, id, price, timeToLive) = { Id = id; OccurredOn = currentTimeMillis (); TimeToLive = timeToLive; ItemId = itemId; Price = price }

let inline isExpired (message: ^a) = 
    let occurredOn = (^a: (member get_OccurredOn: unit -> int64) message)
    let timeToLive = (^a: (member get_TimeToLive: unit -> int64) message)
    let elapsed = currentTimeMillis () - occurredOn
    elapsed > timeToLive

let purchaseRouter purchaseAgent (mailbox: Actor<_>) =
    let random = Random ()
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let millis = random.Next 100 + 1
        printfn "PurchaseRouter: delaying delivery of %A for %i milliseconds" message millis
        let duration = TimeSpan.FromMilliseconds <| float millis
        mailbox.Context.System.Scheduler.ScheduleTellOnce (duration, purchaseAgent, message)
        return! loop ()
    }
    loop ()

let purchaseAgent (mailbox: Actor<PlaceOrder>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        if isExpired message then
            mailbox.Context.System.DeadLetters <! message
            printfn "PurchaseAgent: delivered expired %A to dead letters" message
        else
            printfn "PurchaseAgent: placing order for %A" message
        return! loop ()
    }
    loop ()

let purchaseAgentRef = spawn system "purchaseAgent" purchaseAgent
let purchaseRouterRef = spawn system "purchaseRouter" <| purchaseRouter purchaseAgentRef

purchaseRouterRef <! PlaceOrder.Create ("1", "11", (Money 50.00m), 1000L)
purchaseRouterRef <! PlaceOrder.Create ("2", "22", (Money 250.00m), 100L)
purchaseRouterRef <! PlaceOrder.Create ("3", "33", (Money 32.95m), 10L)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/MessageExpiration.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Format Indicator

```fsharp
type ExecuteBuyOrder = { PortfolioId: string; Symbol: string; Quantity: int; Price: Money; DateTimeOrdered: DateTimeOffset option; Version: int } with
    static member CreateV1 (portfolioId, symbol, quantity, price) = { PortfolioId = portfolioId; Symbol = symbol; Quantity = quantity; Price = price; DateTimeOrdered = None; Version = 1 } 
    static member CreateV2 (portfolioId, symbol, quantity, price) = { PortfolioId = portfolioId; Symbol = symbol; Quantity = quantity; Price = price; DateTimeOrdered = Some DateTimeOffset.UtcNow; Version = 2 } 

let stockTrader (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let orderExecutionStartedOn = match message.Version with
                                      | 1 -> Some DateTimeOffset.UtcNow
                                      | _ -> message.DateTimeOrdered
        printfn "StockTrader: orderExecutionStartedOn: %A" orderExecutionStartedOn
        return! loop () 
    }
    loop ()

let stockTraderRef = spawn system "stockTrader" stockTrader
stockTraderRef <! ExecuteBuyOrder.CreateV1 ("1", "11", 10, (Money 50.00m))
stockTraderRef <! ExecuteBuyOrder.CreateV2 ("1", "11", 10, (Money 50.00m))
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageConstruction/FormatIndicator.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)
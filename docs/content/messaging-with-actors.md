#Messaging with Actors

##Sections

1. [Introduction](index.html)
2. **Messaging with Actors**
	- [Message Channel](#Message-Channel)
	- [Message](#Message)
	- [Pipes and Filters](#Pipes-and-Filters)
	- [Message Router](#Message-Router)
	- [Message Translator](#Message-Translator)
	- [Message Endpoint](#Message-Endpoint)
3. [Messaging Channels](messaging-channels.html)
4. [Message Construction](message-construction.html)
5. [Message Routing](message-routing.html)
6. [Message Transformation](message-transformation.html)
7. [Message Endpoints](message-endpoints.html)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Message Channel

The Message Channel represents the way a consumer and a producer communicate. When using Akka.net, there is nothing special you need to do to implement this pattern as the actor's mailbox implements it for you. 

```fsharp
type ProcessorMessage = ProcessJob of int * int * int

let processor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! ProcessJob(x,y,z) = mailbox.Receive ()
        printfn "Processor: received ProcessJob %i %i %i" x y z
        return! loop ()
    }
    loop ()

let processorRef = spawn system "processor" processor

processorRef <! ProcessJob(1, 3, 5)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/MessageChannel.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message

Messages are sent between actors, they don't have any metadata or header (at least not from the actor point of view), they are just plain data and can be any F# type.

Example using multiple Primitive Types:

```fsharp
let scalarValuePrinter (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? string as msg -> printfn "ScalarValuePrinter: received String %s" msg
        | :? int as msg -> printfn "ScalarValuePrinter: received Int %i" msg
        | _ -> ()
        return! loop ()
    }
    loop ()
```

Example using Discriminated Unions:

```fsharp
type OrderProcessorCommand =
    | ExecuteBuyOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money
type OrderProcessorEvent =
    | BuyOrderExecuted of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | SellOrderExecuted of portfolioId: string * symbol: Symbol * quantity: int * price: Money

let orderProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(i, s, q, p) -> mailbox.Sender () <! BuyOrderExecuted(i, s, q, p)
        | ExecuteSellOrder(i, s, q, p) -> mailbox.Sender () <! SellOrderExecuted(i ,s, q, p)
        return! loop ()
    }
    loop ()
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/Message.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Pipes and Filters

```fsharp
type ProcessIncomingOrder = ProcessIncomingOrder of byte array

let orderAcceptanceEndpoint nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let text = Encoding.Default.GetString message
        printfn "OrderAcceptanceEndpoint: processing %s" text
        nextFilter <! ProcessIncomingOrder(message)
        return! loop ()
    }
    loop ()

let decrypter nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "Decrypter: processing %s" text
        let orderText = text.Replace ("(encryption)", String.Empty)
        nextFilter <! ProcessIncomingOrder(Encoding.Default.GetBytes orderText)
        return! loop ()
    }
    loop ()

let authenticator nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "Authenticator: processing %s" text
        let orderText = text.Replace ("(certificate)", String.Empty)
        nextFilter <! ProcessIncomingOrder(Encoding.Default.GetBytes orderText)
        return! loop ()
    }
    loop ()

let deduplicator nextFilter (mailbox: Actor<_>) =
    let orderIdFrom (orderText: string) =
        let orderIdIndex = orderText.IndexOf ("id='") + 4
        let orderIdLastIndex = orderText.IndexOf ("'", orderIdIndex)
        orderText.Substring (orderIdIndex, orderIdLastIndex)

    let rec loop (processedOrderIds: string Set) = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "Deduplicator: processing %s" text
        let orderId = orderIdFrom text
        if (not <| Set.contains orderId processedOrderIds) then 
            nextFilter <! ProcessIncomingOrder(bytes) 
            return! loop <| Set.add orderId processedOrderIds
        else 
            printfn "Deduplicator: found duplicate order %s" orderId
            return! loop processedOrderIds
    }
    loop Set.empty

let orderManagerSystem (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "OrderManagementSystem: processing unique order: %s" text
        return! loop ()
    }
    loop ()

let orderText = "(encryption)(certificate)<order id='123'>...</order>"
let rawOrderBytes = Encoding.Default.GetBytes orderText

let filter5 = spawn system "orderManagementSystem" orderManagerSystem
let filter4 = spawn system "deduplicator" <| deduplicator filter5
let filter3 = spawn system "authenticator" <| authenticator filter4
let filter2 = spawn system "decrypter" <| decrypter filter3
let filter1 = spawn system "orderAcceptanceEndpoint" <| orderAcceptanceEndpoint filter2

filter1 <! rawOrderBytes
filter1 <! rawOrderBytes
```
<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/PipesAndFilters.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Router

```fsharp
let alternatingRouter (processor1: IActorRef) (processor2: IActorRef) (mailbox: Actor<_>) =
    let rec loop alternate = actor {
        let alternateProcessor () = if alternate = 1 then processor1, 2 else processor2, 1
        let! message = mailbox.Receive ()
        let processor, nextAlternate = alternateProcessor ()
        printfn "AlternatingRouter: routing %O to %s" message processor.Path.Name
        processor <! message
        return! loop nextAlternate
    }
    loop 1
```
<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/MessageRouter.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Translator

```fsharp
// No code example
```
[Sections](#Sections)

##Message Endpoint

```fsharp
type QuoteMessage =
    | RequestPriceQuote of retailerId: string * rfqId: string * itemId: string
    | DiscountPriceCalculated of requestedBy: IActorRef * retailerId: string * rfqId: string * itemId: string * retailPrice: decimal * discountPrice: decimal
type CalculatedDiscountPriceFor = CalculatedDiscountPriceFor of requester: IActorRef * retailerId: string * rfqId: string * itemId: string
type PriceQuote = PriceQuote of quoterId: string * retailerId: string * rfqId: string * itemId: string * retailPrice: decimal * discountPrice: decimal

let highSierraPriceQuotes discounter (mailbox: Actor<_>) =
    let quoterId = mailbox.Self.Path.Name
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | RequestPriceQuote(retailerId, rfqId, itemId) -> 
            printfn "HighSierraPriceQuotes: RequestPriceQuote received" 
            discounter <! CalculatedDiscountPriceFor(mailbox.Sender (), retailerId, rfqId, itemId)
        | DiscountPriceCalculated(requestedBy, retailerId, rfqId, itemId, retailPrice, discountPrice) -> 
            printfn "HighSierraPriceQuotes: DiscountPriceCalculated received" 
            requestedBy <! PriceQuote(quoterId, retailerId, rfqId, itemId, retailPrice, discountPrice)
        return! loop ()
    }
    loop ()

let discounter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! CalculatedDiscountPriceFor(requester, retailerId, rfqId, itemId) = mailbox.Receive ()
        printfn "Discounter: CalculatedDiscountPriceFor received" 
        mailbox.Sender () <! DiscountPriceCalculated(requester, retailerId, rfqId, itemId, 100m, 89.99m)
        return! loop ()
    }
    loop ()

let requester quotes (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! PriceQuote(_, _, _, _,retailPrice, discountPrice) = mailbox.Receive ()
        printfn "Requester: PriceQuote received, retailPrice: %M, discountPrice %M" retailPrice discountPrice 
        return! loop ()
    }
    quotes <! RequestPriceQuote("retailer1", "rfq1", "item1")
    loop ()

let discounterRef = spawn system "discounter" discounter
let highSierraPriceQuotesRef = spawn system "highSierraPriceQuotes" <| highSierraPriceQuotes discounterRef
let requesterRef = spawn system "requester" <| requester highSierraPriceQuotesRef
```
<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/MessageEndpoint.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)
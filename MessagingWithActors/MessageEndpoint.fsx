#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

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
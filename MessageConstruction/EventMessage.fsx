
#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
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

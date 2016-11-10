#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
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
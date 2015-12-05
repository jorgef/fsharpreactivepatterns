#load "..\References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
type RetailItem = { ItemId: string; RetailPrice: decimal }
type RequestForQuotation = { RfqId: string; RetailItems: RetailItem list } with
    member this.TotalRetailPrice with get () = this.RetailItems |> List.sumBy (fun i -> i.RetailPrice)
type RequestPriceQuote = { RfqId: string; ItemId: string; RetailPrice: Money; OrderTotalRetailPrice: Money }
type PriceQuote = { RfqId: string; ItemId: string; RetailPrice: Money; DiscountPrice: Money }
type AggregatorMessage = 
    | PriceQuoteFulfilled of PriceQuote
    | PriceQuoteTimedOut of rfqId: string
    | RequiredPriceQuotesForFulfillment of rfqId: string * quotesRequested: int
type QuotationFulfillment = { RfqId: string; QuotesRequested: int; PriceQuotes: PriceQuote list; Requester: IActorRef }
type BestPriceQuotation = { RfqId: string; PriceQuotes: PriceQuote list }
type SubscribeToPriceQuoteRequests = SubscribeToPriceQuoteRequests of quoterId: string * quoteProcessor: IActorRef

let priceQuoteAggregator (mailbox: Actor<_>) =
    let rec loop fulfilledPriceQuotes = actor {
        let bestPriceQuotationFrom (quotationFulfillment: QuotationFulfillment) =
            let bestPrices = 
                quotationFulfillment.PriceQuotes
                |> List.groupBy (fun priceQuote -> priceQuote.ItemId)
                |> List.map (fun (itemId, quotes) -> 
                    quotes 
                    |> List.maxBy (fun quote ->  
                        let (Money discount) = quote.DiscountPrice
                        discount))
            { RfqId = quotationFulfillment.RfqId; PriceQuotes = bestPrices }
        
        let quoteBestPrice (quotationFulfillment: QuotationFulfillment) = 
            fulfilledPriceQuotes 
            |> Map.tryFind quotationFulfillment.RfqId
            |> Option.map (fun q -> quotationFulfillment.Requester <! bestPriceQuotationFrom quotationFulfillment)
            |> Option.fold (fun _ _ -> fulfilledPriceQuotes |> Map.remove quotationFulfillment.RfqId) fulfilledPriceQuotes

        let priceQuoteRequestTimedOut rfqId = 
            fulfilledPriceQuotes 
            |> Map.tryFind rfqId
            |> Option.fold (fun _ _ -> quoteBestPrice (fulfilledPriceQuotes |> Map.find rfqId)) fulfilledPriceQuotes

        let priceQuoteRequestFulfilled (priceQuoteFulfilled: PriceQuote) =
            let previousFulfillment = fulfilledPriceQuotes |> Map.find priceQuoteFulfilled.RfqId
            let currentPriceQuotes = previousFulfillment.PriceQuotes @ [priceQuoteFulfilled]
            let currentFulfillment = { previousFulfillment with PriceQuotes = currentPriceQuotes }
            if (currentPriceQuotes.Length >= currentFulfillment.QuotesRequested) then quoteBestPrice currentFulfillment
            else fulfilledPriceQuotes |> Map.add priceQuoteFulfilled.RfqId currentFulfillment

        let! message = mailbox.Receive ()
        match message with
        | RequiredPriceQuotesForFulfillment(rfqId, quotesRequested) as message ->
            printfn "PriceQuoteAggregator: required fulfilled: %A" message
            let duration = TimeSpan.FromSeconds 2.
            mailbox.Context.System.Scheduler.ScheduleTellOnce (duration, mailbox.Self, PriceQuoteTimedOut rfqId)
            return! loop (fulfilledPriceQuotes |> Map.add rfqId ({RfqId = rfqId; QuotesRequested = quotesRequested; PriceQuotes = []; Requester = mailbox.Sender () }))
        | PriceQuoteFulfilled priceQuote -> 
            printfn "PriceQuoteAggregator: fulfilled price quote: %A" priceQuote
            return! loop <| priceQuoteRequestFulfilled priceQuote
        | PriceQuoteTimedOut rfqId -> return! loop <| priceQuoteRequestTimedOut rfqId
    }
    loop Map.empty

let mountaineeringSuppliesOrderProcessor priceQuoteAggregator (mailbox: Actor<_>) =
    let rec loop subscribers = actor {
        let dispatch rfq =
            subscribers
            |> Map.toList
            |> List.iter (fun (_, (SubscribeToPriceQuoteRequests(quoterId, quoteProcessor) as subscriber)) ->
                rfq.RetailItems
                |> List.iter (fun retailItem -> 
                    printfn "OrderProcessor: %s item: %s to: %s" rfq.RfqId retailItem.ItemId quoterId
                    quoteProcessor <! { RfqId = rfq.RfqId; ItemId = retailItem.ItemId; RetailPrice = Money retailItem.RetailPrice; OrderTotalRetailPrice = Money rfq.TotalRetailPrice }))

        let! message = mailbox.Receive ()
        match box message with
        | :? SubscribeToPriceQuoteRequests as subscriber -> 
            let (SubscribeToPriceQuoteRequests(quoterId, quoteProcessor)) = subscriber
            return! loop <| Map.add quoteProcessor.Path.Name subscriber subscribers 
        | :? PriceQuote as priceQuote -> 
            priceQuoteAggregator <! PriceQuoteFulfilled priceQuote
            printfn "OrderProcessor: received: %A" priceQuote
        | :? RequestForQuotation as rfq -> 
            priceQuoteAggregator <! RequiredPriceQuotesForFulfillment(rfq.RfqId, subscribers.Count * rfq.RetailItems.Length)
            dispatch rfq
        | :? BestPriceQuotation as bestPriceQuotation -> printfn "OrderProcessor: received: %A" bestPriceQuotation
        | message -> printfn "OrderProcessor: unexpected: %A" message
        return! loop subscribers 
    }
    loop Map.empty

let budgetHikersPriceQuotes priceQuoteRequestPublisher (mailbox: Actor<RequestPriceQuote>) =
    let quoterId = mailbox.Self.Path.Name
    priceQuoteRequestPublisher <! SubscribeToPriceQuoteRequests(quoterId, mailbox.Self)

    let discountPercentage orderTotalRetailPrice =
        if (orderTotalRetailPrice <= 100.00m) then 0.02m
        elif (orderTotalRetailPrice <= 399.99m) then 0.03m
        elif (orderTotalRetailPrice <= 499.99m) then 0.05m
        elif (orderTotalRetailPrice <= 799.99m) then 0.07m
        else 0.075m
    
    let rec loop () = actor {
        let! rpq = mailbox.Receive ()
        let (Money retailPrice) = rpq.RetailPrice
        let (Money orderTotalRetailPrice) = rpq.OrderTotalRetailPrice
        let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
        mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        return! loop ()
    }
    loop ()

let highSierraPriceQuotes priceQuoteRequestPublisher (mailbox: Actor<RequestPriceQuote>) =
    let quoterId = mailbox.Self.Path.Name
    priceQuoteRequestPublisher <! SubscribeToPriceQuoteRequests(quoterId, mailbox.Self)
    
    let discountPercentage orderTotalRetailPrice =
        if (orderTotalRetailPrice <= 150.00m) then 0.015m
        elif (orderTotalRetailPrice <= 499.99m) then 0.02m
        elif (orderTotalRetailPrice <= 999.99m) then 0.03m
        elif (orderTotalRetailPrice <= 4999.99m) then 0.04m
        else 0.05m
    
    let rec loop () = actor {
        let! rpq = mailbox.Receive ()
        let (Money retailPrice) = rpq.RetailPrice
        let (Money orderTotalRetailPrice) = rpq.OrderTotalRetailPrice
        if orderTotalRetailPrice < 1000.00m then
            let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
            mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        else printfn "BudgetHikersPriceQuotes: ignoring: %A" rpq
        return! loop ()
    }
    loop ()

let mountainAscentPriceQuotes priceQuoteRequestPublisher (mailbox: Actor<RequestPriceQuote>) =
    let quoterId = mailbox.Self.Path.Name
    priceQuoteRequestPublisher <! SubscribeToPriceQuoteRequests(quoterId, mailbox.Self)
    
    let discountPercentage orderTotalRetailPrice =
        if (orderTotalRetailPrice <= 99.99m) then 0.01m
        elif (orderTotalRetailPrice <= 199.99m) then 0.02m
        elif (orderTotalRetailPrice <= 499.99m) then 0.03m
        elif (orderTotalRetailPrice <= 799.99m) then 0.04m
        elif (orderTotalRetailPrice <= 999.99m) then 0.05m
        elif (orderTotalRetailPrice <= 2999.99m) then 0.0475m
        else 0.05m
    
    let rec loop () = actor {
        let! rpq = mailbox.Receive ()
        let (Money retailPrice) = rpq.RetailPrice
        let (Money orderTotalRetailPrice) = rpq.OrderTotalRetailPrice
        let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
        mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        return! loop ()
    }
    loop ()

let pinnacleGearPriceQuotes priceQuoteRequestPublisher (mailbox: Actor<RequestPriceQuote>) =
    let quoterId = mailbox.Self.Path.Name
    priceQuoteRequestPublisher <! SubscribeToPriceQuoteRequests(quoterId, mailbox.Self)
    
    let discountPercentage orderTotalRetailPrice =
        if (orderTotalRetailPrice <= 299.99m) then 0.015m
        elif (orderTotalRetailPrice <= 399.99m) then 0.0175m
        elif (orderTotalRetailPrice <= 499.99m) then 0.02m
        elif (orderTotalRetailPrice <= 999.99m) then 0.03m
        elif (orderTotalRetailPrice <= 1199.99m) then 0.035m
        elif (orderTotalRetailPrice <= 4999.99m) then 0.04m
        elif (orderTotalRetailPrice <= 7999.99m) then 0.05m
        else 0.06m
    
    let rec loop () = actor {
        let! rpq = mailbox.Receive ()
        let (Money retailPrice) = rpq.RetailPrice
        let (Money orderTotalRetailPrice) = rpq.OrderTotalRetailPrice
        let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
        mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        return! loop ()
    }
    loop ()

let rockBottomOuterwearPriceQuotes priceQuoteRequestPublisher (mailbox: Actor<RequestPriceQuote>) =
    let quoterId = mailbox.Self.Path.Name
    priceQuoteRequestPublisher <! SubscribeToPriceQuoteRequests(quoterId, mailbox.Self)
    
    let discountPercentage orderTotalRetailPrice =
        if (orderTotalRetailPrice <= 100.00m) then 0.015m
        elif (orderTotalRetailPrice <= 399.99m) then 0.02m
        elif (orderTotalRetailPrice <= 499.99m) then 0.03m
        elif (orderTotalRetailPrice <= 799.99m) then 0.04m
        elif (orderTotalRetailPrice <= 999.99m) then 0.05m
        elif (orderTotalRetailPrice <= 2999.99m) then 0.06m
        elif (orderTotalRetailPrice <= 4999.99m) then 0.07m
        elif (orderTotalRetailPrice <= 5999.99m) then 0.075m
        else 0.08m
    
    let rec loop () = actor {
        let! rpq = mailbox.Receive ()
        let (Money retailPrice) = rpq.RetailPrice
        let (Money orderTotalRetailPrice) = rpq.OrderTotalRetailPrice
        if orderTotalRetailPrice < 1000.00m then
            let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
            mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        else printfn "RockBottomOuterwearPriceQuotes: ignoring: %A" rpq
        return! loop ()
    }
    loop ()

let priceQuoteAggregatorRef = spawn system "priceQuoteAggregator" priceQuoteAggregator
let orderProcessorRef = spawn system "orderProcessor" <| mountaineeringSuppliesOrderProcessor priceQuoteAggregatorRef
let budgetHikersRef = spawn system "budgetHikersPriceQuotes" <| budgetHikersPriceQuotes orderProcessorRef
let highSierraRef = spawn system "highSierra" <| highSierraPriceQuotes orderProcessorRef
let mountainAscentRef = spawn system "mountainAscent" <| mountainAscentPriceQuotes orderProcessorRef
let pinnacleGearRef = spawn system "pinnacleGear" <| pinnacleGearPriceQuotes orderProcessorRef
let rockBottomOuterwearRef = spawn system "rockBottomOuterwear" <| rockBottomOuterwearPriceQuotes orderProcessorRef

orderProcessorRef <! { RfqId = "123"; RetailItems = [ { ItemId = "1"; RetailPrice = 29.95m }; { ItemId = "2"; RetailPrice = 99.95m }; { ItemId = "3"; RetailPrice = 14.95m } ] }
orderProcessorRef <! { RfqId = "125"; RetailItems = [ { ItemId = "4"; RetailPrice = 39.99m }; { ItemId = "5"; RetailPrice = 199.95m }; { ItemId = "6"; RetailPrice = 149.95m }; { ItemId = "7"; RetailPrice = 724.99m } ] }
orderProcessorRef <! { RfqId = "129"; RetailItems = [ { ItemId = "8"; RetailPrice = 119.99m }; { ItemId = "9"; RetailPrice = 499.95m }; { ItemId = "10"; RetailPrice = 519.00m }; { ItemId = "11"; RetailPrice = 209.50m } ] }
orderProcessorRef <! { RfqId = "135"; RetailItems = [ { ItemId = "12"; RetailPrice = 0.97m }; { ItemId = "13"; RetailPrice = 9.50m }; { ItemId = "14"; RetailPrice = 1.99m } ] }
orderProcessorRef <! { RfqId = "140"; RetailItems = [ { ItemId = "15"; RetailPrice = 107.50m }; { ItemId = "16"; RetailPrice = 9.50m }; { ItemId = "17"; RetailPrice = 599.99m }; { ItemId = "18"; RetailPrice = 249.95m }; { ItemId = "19"; RetailPrice = 789.99m } ] }
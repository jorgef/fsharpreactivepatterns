#load "..\References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
type RetailItem = { ItemId: string; RetailPrice: decimal }
type PriceQuoteInterest = { Path: string; QuoteProcessor: IActorRef; LowTotalRetail: Money; HighTotalRetail: Money }
type RequestForQuotation = { RfqId: string; RetailItems: RetailItem list } with
    member this.TotalRetailPrice with get () = this.RetailItems |> List.sumBy (fun i -> i.RetailPrice)
type RequestPriceQuote = { RfqId: string; ItemId: string; RetailPrice: Money; OrderTotalRetailPrice: Money }
type PriceQuote = { RfqId: string; ItemId: string; RetailPrice: Money; DiscountPrice: Money }

let mountaineeringSuppliesOrderProcessor (mailbox: Actor<_>) =
    let rec loop interestRegistry = actor {
        let calculateRecipientList (rfq: RequestForQuotation) = 
            interestRegistry
            |> Map.toList
            |> List.filter (fun (_, v) -> 
                let (Money lowTotalRetail) = v.LowTotalRetail
                let (Money highTotalRetail) = v.HighTotalRetail
                rfq.TotalRetailPrice >= lowTotalRetail && rfq.TotalRetailPrice <= highTotalRetail)
            |> List.map (fun (_, v) -> v.QuoteProcessor)

        let dispatchTo rfq (recipientList: IActorRef list) =
            recipientList
            |> List.iter (fun recipient ->
                rfq.RetailItems
                |> List.iter (fun retailItem -> 
                    printfn "OrderProcessor: %s item: %s to: %s" rfq.RfqId retailItem.ItemId <| recipient.Path.ToString ()
                    recipient <! { RfqId = rfq.RfqId; ItemId = retailItem.ItemId; RetailPrice = Money retailItem.RetailPrice; OrderTotalRetailPrice = Money rfq.TotalRetailPrice }))

        let! message = mailbox.Receive ()
        match box message with
        | :? PriceQuoteInterest as interest -> return! loop <| Map.add interest.Path interest interestRegistry 
        | :? PriceQuote as priceQuote -> printfn "OrderProcessor: received: %A" priceQuote
        | :? RequestForQuotation as rfq -> 
            let recipientList = calculateRecipientList rfq
            dispatchTo rfq recipientList
        | message -> printfn "OrderProcessor: unexpected: %A" message
        return! loop interestRegistry 
    }
    loop Map.empty

let budgetHikersPriceQuotes interestRegistrar (mailbox: Actor<RequestPriceQuote>) =
    interestRegistrar <! { Path = mailbox.Self.Path.ToString (); QuoteProcessor = mailbox.Self; LowTotalRetail = Money 1.00m; HighTotalRetail = Money 1000.00m }
    
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

let highSierraPriceQuotes interestRegistrar (mailbox: Actor<RequestPriceQuote>) =
    interestRegistrar <! { Path = mailbox.Self.Path.ToString (); QuoteProcessor = mailbox.Self; LowTotalRetail = Money 100.00m; HighTotalRetail = Money 10000.00m }
    
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
        let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
        mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        return! loop ()
    }
    loop ()

let mountainAscentPriceQuotes interestRegistrar (mailbox: Actor<RequestPriceQuote>) =
    interestRegistrar <! { Path = mailbox.Self.Path.ToString (); QuoteProcessor = mailbox.Self; LowTotalRetail = Money 70.00m; HighTotalRetail = Money 5000.00m }
    
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

let pinnacleGearPriceQuotes interestRegistrar (mailbox: Actor<RequestPriceQuote>) =
    interestRegistrar <! { Path = mailbox.Self.Path.ToString (); QuoteProcessor = mailbox.Self; LowTotalRetail = Money 250.00m; HighTotalRetail = Money 500000.00m }
    
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

let rockBottomOuterwearPriceQuotes interestRegistrar (mailbox: Actor<RequestPriceQuote>) =
    interestRegistrar <! { Path = mailbox.Self.Path.ToString (); QuoteProcessor = mailbox.Self; LowTotalRetail = Money 0.50m; HighTotalRetail = Money 7500.00m }
    
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
        let discount = discountPercentage (orderTotalRetailPrice * retailPrice)
        mailbox.Sender () <! { RfqId = rpq.RfqId; ItemId = rpq.ItemId; RetailPrice = rpq.RetailPrice; DiscountPrice = Money (retailPrice - discount) }
        return! loop ()
    }
    loop ()

let orderProcessorRef = spawn system "orderProcessor" mountaineeringSuppliesOrderProcessor
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
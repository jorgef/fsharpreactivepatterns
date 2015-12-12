#Message Construction

##Sections

1. [Introduction](index.html)
2. [Messaging with Actors](messaging-with-actors.html)
3. [Messaging Channels](messaging-channels.html)
4. [Message Construction](message-construction.html)
5. **Message Routing**
	- [Content-Based Router](#Content-Based-Router)
	- [Message Filter](#Message-Filter)
	- [Dynamic Router](#Dynamic-Router)
	- [Recipient List](#Recipient-List)
	- [Splitter](#Splitter)
	- [Aggregator](#Aggregator)
	- [Resequencer](#Resequencer)
	- [Composed Message Processor](#Composed-Message-Processor)
	- [Scatter-Gather](#Scatter-Gather)
	- [Routing Slip](#Routing-Slip)
	- [Process Manager](#Process-Manager)
	- [Message Broker](#Message-Broker)
6. [Message Transformation](message-transformation.html)
7. [Message Endpoints](message-endpoints.html)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Content-Based Router

```fsharp
type OrderItem = { Id: string; ItemType: string; Description: string; Price: decimal }
type Order = { Id: string; OrderType: string; OrderItems: Map<string, OrderItem> }
type OrderPlaced = OrderPlaced of Order

let inventorySystemA (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "InventorySystemA: handling %A" message
        return! loop ()
    }
    loop ()

let inventorySystemX (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "InventorySystemX: handling %A" message
        return! loop ()
    }
    loop ()

let orderRouter (mailbox: Actor<_>) =
    let inventorySystemA = spawn mailbox.Context "inventorySystemA" inventorySystemA
    let inventorySystemX = spawn mailbox.Context "inventorySystemX" inventorySystemX

    let rec loop () = actor {
        let! orderPlaced = mailbox.Receive ()
        let (OrderPlaced order) = orderPlaced
        match order.OrderType with
        | "TypeABC" -> 
            printfn "OrderRouter: routing %A" orderPlaced
            inventorySystemA <! orderPlaced
        | "TypeXYZ" -> 
            printfn "OrderRouter: routing %A" orderPlaced
            inventorySystemX <! orderPlaced
        | _ -> printfn "OrderRouter: received unexpected message"
        return! loop ()
    }
    loop ()

let orderRouterRef = spawn system "orderRouter" orderRouter

let orderItem1 = { Id = "1"; ItemType = "TypeABC.4"; Description = "An item of type ABC.4."; Price = 29.95m }
let orderItem2 = { Id = "2"; ItemType = "TypeABC.1"; Description = "An item of type ABC.1."; Price = 99.95m }
let orderItem3 = { Id = "3"; ItemType = "TypeABC.9"; Description = "An item of type ABC.9."; Price = 14.95m }
let orderItemsOfTypeA = Map.ofList [(orderItem1.ItemType, orderItem1); (orderItem2.ItemType, orderItem2); (orderItem3.ItemType, orderItem3)]
orderRouterRef <! OrderPlaced({ Id = "123"; OrderType = "TypeABC"; OrderItems = orderItemsOfTypeA })

let orderItem4 = { Id = "4"; ItemType = "TypeXYZ.2"; Description = "An item of type XYZ.2."; Price = 74.95m }
let orderItem5 = { Id = "5"; ItemType = "TypeXYZ.1"; Description = "An item of type XYZ.1."; Price = 59.95m }
let orderItem6 = { Id = "6"; ItemType = "TypeXYZ.7"; Description = "An item of type XYZ.7."; Price = 29.95m }
let orderItem7 = { Id = "7"; ItemType = "TypeXYZ.5"; Description = "An item of type XYZ.5."; Price = 9.95m }
let orderItemsOfTypeX = Map.ofList [(orderItem4.ItemType, orderItem4); (orderItem5.ItemType, orderItem5); (orderItem6.ItemType, orderItem6); (orderItem7.ItemType, orderItem7)]
orderRouterRef <! OrderPlaced({ Id = "124"; OrderType = "TypeXYZ"; OrderItems = orderItemsOfTypeX })
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/ContentBasedRouter.fsx" target="_blank">Complete Code</a> 

[Sections](#Sections)

##Message Filter

```fsharp
type OrderItem = { Id: string; ItemType: string; Description: string; Price: decimal }
type Order = { Id: string; OrderType: string; OrderItems: Map<string, OrderItem> }
type OrderPlaced = OrderPlaced of Order

let inventorySystemA (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "InventorySystemA: handling %A" message
        return! loop ()
    }
    loop ()

let inventorySystemX (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "InventorySystemX: handling %A" message
        return! loop ()
    }
    loop ()

let inventorySystemXMessageFilter (actualInventorySystemX: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! orderPlaced = mailbox.Receive ()
        let (OrderPlaced order) = orderPlaced
        match order.OrderType with
        | "TypeABC" -> actualInventorySystemX.Forward orderPlaced
        | _ -> printfn "InventorySystemXMessageFilter: filtering: %A" orderPlaced
        return! loop ()
    }
    loop ()

let inventorySystemARef = spawn system "inventorySystemA" inventorySystemA
let actualInventorySystemXRef = spawn system "inventorySystemX" inventorySystemX
let inventorySystemXRef = spawn system "inventorySystemXMessageFilter" <| inventorySystemXMessageFilter actualInventorySystemXRef

let orderItem1 = { Id = "1"; ItemType = "TypeABC.4"; Description = "An item of type ABC.4."; Price = 29.95m }
let orderItem2 = { Id = "2"; ItemType = "TypeABC.1"; Description = "An item of type ABC.1."; Price = 99.95m }
let orderItem3 = { Id = "3"; ItemType = "TypeABC.9"; Description = "An item of type ABC.9."; Price = 14.95m }
let orderItemsOfTypeA = Map.ofList [(orderItem1.ItemType, orderItem1); (orderItem2.ItemType, orderItem2); (orderItem3.ItemType, orderItem3)]
inventorySystemARef <! OrderPlaced({ Id = "123"; OrderType = "TypeABC"; OrderItems = orderItemsOfTypeA })
inventorySystemXRef <! OrderPlaced({ Id = "123"; OrderType = "TypeABC"; OrderItems = orderItemsOfTypeA })

let orderItem4 = { Id = "4"; ItemType = "TypeXYZ.2"; Description = "An item of type XYZ.2."; Price = 74.95m }
let orderItem5 = { Id = "5"; ItemType = "TypeXYZ.1"; Description = "An item of type XYZ.1."; Price = 59.95m }
let orderItem6 = { Id = "6"; ItemType = "TypeXYZ.7"; Description = "An item of type XYZ.7."; Price = 29.95m }
let orderItem7 = { Id = "7"; ItemType = "TypeXYZ.5"; Description = "An item of type XYZ.5."; Price = 9.95m }
let orderItemsOfTypeX = Map.ofList [(orderItem4.ItemType, orderItem4); (orderItem5.ItemType, orderItem5); (orderItem6.ItemType, orderItem6); (orderItem7.ItemType, orderItem7)]
inventorySystemARef <! OrderPlaced({ Id = "124"; OrderType = "TypeXYZ"; OrderItems = orderItemsOfTypeX })
inventorySystemXRef <! OrderPlaced({ Id = "124"; OrderType = "TypeXYZ"; OrderItems = orderItemsOfTypeX })
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/MessageFilter.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Dynamic Router

```fsharp
type Registration = 
    | InterestedIn of string
    | NoLongerInterestedIn of string
type TypeAMessage = TypeAMessage of string
type TypeBMessage = TypeBMessage of string
type TypeCMessage = TypeCMessage of string
type TypeDMessage = TypeDMessage of string

let typeAInterested interestRouter (mailbox: Actor<TypeAMessage>) =
    interestRouter <! InterestedIn(typeof<TypeAMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeAInterested: received: %A" message
        return! loop ()
    }
    loop ()

let typeBInterested interestRouter (mailbox: Actor<TypeBMessage>) =
    interestRouter <! InterestedIn(typeof<TypeBMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeBInterested: received: %A" message
        return! loop ()
    }
    loop ()

let typeCInterested interestRouter (mailbox: Actor<TypeCMessage>) =
    interestRouter <! InterestedIn(typeof<TypeCMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeCInterested: received: %A" message
        interestRouter <! NoLongerInterestedIn(typeof<TypeCMessage>.Name)
        return! loop ()
    }
    loop ()

let typeCAlsoInterested interestRouter (mailbox: Actor<TypeCMessage>) =
    interestRouter <! InterestedIn(typeof<TypeCMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeCAlsoInterested: received: %A" message
        interestRouter <! NoLongerInterestedIn(typeof<TypeCMessage>.Name)
        return! loop ()
    }
    loop ()

let dunnoInterested (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "DunnoInterest: received undeliverable message: %A" message
        return! loop ()
    }
    loop ()

let typedMessageInterestRouter dunnoInterested (mailbox: Actor<_>) =

    let rec loop (interestRegistry: Map<string, IActorRef>) (secondaryInterestRegistry: Map<string, IActorRef>) = actor {

        let registerInterest messageType interested =
            interestRegistry
            |> Map.tryFind messageType
            |> Option.fold 
                (fun _ _ -> interestRegistry, Map.add messageType interested secondaryInterestRegistry) 
                (Map.add messageType interested interestRegistry, secondaryInterestRegistry)

        let unregisterInterest messageType wasInterested =
            interestRegistry
            |> Map.tryFind messageType
            |> Option.bind (fun i -> if i = wasInterested then Some (Map.remove messageType interestRegistry, secondaryInterestRegistry) else None)
            |> Option.bind (fun (p,s) -> s |> Map.tryFind messageType |> Option.map(fun i -> Map.add messageType i p,  Map.remove messageType secondaryInterestRegistry))
            |> Option.fold (fun _ i -> i) (interestRegistry, secondaryInterestRegistry)

        let sendFor message =
            let messageType = (message.GetType ()).Name
            interestRegistry
            |> Map.tryFind messageType
            |> Option.fold (fun _ i -> i) dunnoInterested
            |> fun i -> i <! message 

        let! message = mailbox.Receive ()
        match box message with
        | :? Registration as r ->
            match r with
            | InterestedIn messageType -> return! loop <|| registerInterest messageType (mailbox.Sender ())
            | NoLongerInterestedIn messageType -> return! loop <|| unregisterInterest messageType (mailbox.Sender ())
        | message -> 
            sendFor message
            return! loop interestRegistry secondaryInterestRegistry
    }
    loop Map.empty Map.empty

let dunnoInterestedRef = spawn system "dunnoInterested" dunnoInterested
let typedMessageInterestRouterRef = spawn system "typedMessageInterestRouter" <| typedMessageInterestRouter dunnoInterestedRef
let typeAInterestedRef = spawn system "typeAInterest" <| typeAInterested typedMessageInterestRouterRef
let typeBInterestedRef = spawn system "typeBInterest" <| typeBInterested typedMessageInterestRouterRef
let typeCInterestedRef = spawn system "typeCInterest" <| typeCInterested typedMessageInterestRouterRef
let typeCAlsoInterestedRef = spawn system "typeCAlsoInterested" <| typeCAlsoInterested typedMessageInterestRouterRef

typedMessageInterestRouterRef <! TypeAMessage("Message of TypeA.")
typedMessageInterestRouterRef <! TypeBMessage("Message of TypeB.")
typedMessageInterestRouterRef <! TypeCMessage("Message of TypeC.")
typedMessageInterestRouterRef <! TypeCMessage("Another message of TypeC.")
typedMessageInterestRouterRef <! TypeDMessage("Message of TypeD.")
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/DynamicRouter.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Recipient List

```fsharp
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
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/RecipientList.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Splitter

```fsharp
type OrderItem = { Id: string; ItemType: string; Description: string; Price: Money }
type Order = { OrderItems: Map<string, OrderItem> }
type OrderPlaced = OrderPlaced of Order
type TypeAItemOrdered = TypeAItemOrdered of OrderItem
type TypeBItemOrdered = TypeBItemOrdered of OrderItem
type TypeCItemOrdered = TypeCItemOrdered of OrderItem

let orderItemTypeAProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! TypeAItemOrdered orderItem = mailbox.Receive ()
        printfn "OrderItemTypeAProcessor: handling %A" orderItem
        return! loop ()
    }
    loop ()

let orderItemTypeBProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! TypeBItemOrdered orderItem = mailbox.Receive ()
        printfn "OrderItemTypeBProcessor: handling %A" orderItem
        return! loop ()
    }
    loop ()

let orderItemTypeCProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! TypeCItemOrdered orderItem = mailbox.Receive ()
        printfn "OrderItemTypeCProcessor: handling %A" orderItem
        return! loop ()
    }
    loop ()

let orderRouter (mailbox: Actor<_>) =
    let orderItemTypeAProcessor = spawn mailbox.Context "orderItemTypeAProcessor" orderItemTypeAProcessor
    let orderItemTypeBProcessor = spawn mailbox.Context "orderItemTypeBProcessor" orderItemTypeBProcessor
    let orderItemTypeCProcessor = spawn mailbox.Context "orderItemTypeCProcessor" orderItemTypeCProcessor

    let rec loop () = actor {
        let! OrderPlaced order = mailbox.Receive ()
        order.OrderItems 
        |> Map.iter (fun k orderItem -> 
            match orderItem.ItemType with
            | "TypeA" -> 
                printfn "OrderRouter: routing %A" orderItem
                orderItemTypeAProcessor <! TypeAItemOrdered(orderItem)
            | "TypeB" -> 
                printfn "OrderRouter: routing %A" orderItem
                orderItemTypeBProcessor <! TypeBItemOrdered(orderItem)
            | "TypeC" -> 
                printfn "OrderRouter: routing %A" orderItem
                orderItemTypeCProcessor <! TypeCItemOrdered(orderItem)
            | _ -> printfn "OrderRouter: received unexpected message")
        return! loop ()
    }
    loop ()

let splitter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    loop ()

let orderRouterRef = spawn system "orderRouter" orderRouter
let orderItem1 = { Id = "1"; ItemType = "TypeA"; Description = "An item of type A."; Price = Money 23.95m }
let orderItem2 = { Id = "2"; ItemType = "TypeB"; Description = "An item of type B."; Price = Money 99.95m }
let orderItem3 = { Id = "3"; ItemType = "TypeC"; Description = "An item of type C."; Price = Money 14.95m }
let orderItems = Map.ofList [ (orderItem1.Id, orderItem1); (orderItem2.Id, orderItem2); (orderItem3.Id, orderItem3) ]
    
orderRouterRef <! OrderPlaced({ OrderItems = orderItems })
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/Splitter.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Aggregator

```fsharp
type RetailItem = { ItemId: string; RetailPrice: decimal }
type PriceQuoteInterest = { Path: string; QuoteProcessor: IActorRef; LowTotalRetail: Money; HighTotalRetail: Money }
type RequestForQuotation = { RfqId: string; RetailItems: RetailItem list } with
    member this.TotalRetailPrice with get () = this.RetailItems |> List.sumBy (fun i -> i.RetailPrice)
type RequestPriceQuote = { RfqId: string; ItemId: string; RetailPrice: Money; OrderTotalRetailPrice: Money }
type PriceQuote = { RfqId: string; ItemId: string; RetailPrice: Money; DiscountPrice: Money }
type AggregatorMessage = 
    | PriceQuoteFulfilled of PriceQuote
    | RequiredPriceQuotesForFulfillment of rfqId: string * quotesRequested: int
type QuotationFulfillment = { RfqId: string; QuotesRequested: int; PriceQuotes: PriceQuote list; Requester: IActorRef }

let priceQuoteAggregator (mailbox: Actor<_>) =
    let rec loop fulfilledPriceQuotes = actor {
        let! message = mailbox.Receive ()
        match message with
        | RequiredPriceQuotesForFulfillment(rfqId, quotesRequested) as message ->
            printfn "PriceQuoteAggregator: required fulfilled: %A" message
            return! loop (fulfilledPriceQuotes |> Map.add rfqId ({RfqId = rfqId; QuotesRequested = quotesRequested; PriceQuotes = []; Requester = mailbox.Sender () }))
        | PriceQuoteFulfilled priceQuoteFulfilled ->
            printfn "PriceQuoteAggregator: fulfilled price quote: %A" priceQuoteFulfilled
            let previousFulfillment = fulfilledPriceQuotes |> Map.find priceQuoteFulfilled.RfqId
            let currentPriceQuotes = previousFulfillment.PriceQuotes @ [priceQuoteFulfilled]
            let currentFulfillment = { previousFulfillment with PriceQuotes = currentPriceQuotes }
            if (currentPriceQuotes.Length >= currentFulfillment.QuotesRequested) then
                currentFulfillment.Requester <! currentFulfillment
                return! loop (fulfilledPriceQuotes |> Map.remove priceQuoteFulfilled.RfqId)
            else return! loop (fulfilledPriceQuotes |> Map.add priceQuoteFulfilled.RfqId currentFulfillment)
    }
    loop Map.empty

let mountaineeringSuppliesOrderProcessor priceQuoteAggregator (mailbox: Actor<_>) =
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
        | :? PriceQuote as priceQuote -> 
            priceQuoteAggregator <! PriceQuoteFulfilled priceQuote
            printfn "OrderProcessor: received: %A" priceQuote
        | :? RequestForQuotation as rfq -> 
            let recipientList = calculateRecipientList rfq
            priceQuoteAggregator <! RequiredPriceQuotesForFulfillment(rfq.RfqId, recipientList.Length * rfq.RetailItems.Length)
            dispatchTo rfq recipientList
        | :? QuotationFulfillment as fulfillment -> printfn "OrderProcessor: received: %A" fulfillment
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

```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/Aggregator.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Resequencer

```fsharp
type SequencedMessage =  SequencedMessage of correlationId: string * index: int * total: int
type ResequencedMessages = { DispatchableIndex: int; SequencedMessages: SequencedMessage [] } with
    member this.AdvancedTo(dispatchableIndex: int) = { this with DispatchableIndex = dispatchableIndex }

let chaosRouter consumer (mailbox: Actor<_>) =
    let random = Random()
    let rec loop () = actor {
        let! SequencedMessage(correlationId, index, total) as sequencedMessage = mailbox.Receive ()
        let millis = random.Next 100
        printfn "ChaosRouter: delaying delivery of %A for %i milliseconds" sequencedMessage millis
        let duration = TimeSpan.FromMilliseconds (float millis)
        mailbox.Context.System.Scheduler.ScheduleTellOnce (duration, consumer, sequencedMessage)
        return! loop ()
    }
    loop ()

let resequencerConsumer actualConsumer (mailbox: Actor<_>) =
    let rec loop resequenced = actor {
        let dummySequencedMessages count = [| for _ in 1 .. count -> SequencedMessage("", -1, count) |]
        
        let resequence sequencedMessage resequenced = 
            let (SequencedMessage(correlationId, index, total)) = sequencedMessage
            let resequenced = 
                resequenced
                |> Map.tryFind correlationId
                |> Option.fold 
                    (fun _ v -> resequenced) 
                    (resequenced |> Map.add correlationId ( { DispatchableIndex = 1; SequencedMessages = dummySequencedMessages total }))
            resequenced
            |> Map.find correlationId
            |> fun m -> m.SequencedMessages.[index - 1] <- sequencedMessage
            resequenced

        let rec dispatchAllSequenced correlationId resequenced =
            let resequencedMessage = resequenced |> Map.find correlationId
            let dispatchableIndex = 
                resequencedMessage.SequencedMessages 
                |> Array.filter (fun (SequencedMessage(_, index, _) as sequencedMessage) -> index = resequencedMessage.DispatchableIndex)
                |> Array.fold (fun dispatchableIndex sequencedMessage -> 
                        actualConsumer <! sequencedMessage
                        dispatchableIndex + 1) 
                        resequencedMessage.DispatchableIndex
            let resequenced = resequenced |> Map.add correlationId (resequencedMessage.AdvancedTo dispatchableIndex)
            if resequencedMessage.SequencedMessages |> Array.exists (fun (SequencedMessage(_, index, _)) -> index = dispatchableIndex) then dispatchAllSequenced correlationId resequenced
            else resequenced

        let removeCompleted correlationId resequenced = 
            resequenced
            |> Map.find correlationId
            |> fun resequencedMessages -> 
                let (SequencedMessage(_,_,total)) = resequencedMessages.SequencedMessages.[0]
                if resequencedMessages.DispatchableIndex > total then
                    printfn "ResequencerConsumer: removed completed: %s" correlationId
                    resequenced |> Map.remove correlationId
                else resequenced

        let! SequencedMessage(correlationId, index, total) as unsequencedMessage = mailbox.Receive ()
        printfn "ResequencerConsumer: received: %A" unsequencedMessage
        let resequenced = 
            resequenced
            |> resequence unsequencedMessage
            |> dispatchAllSequenced correlationId
            |> removeCompleted correlationId
        return! loop resequenced
    }
    loop Map.empty

let sequencedMessageConsumer (mailbox: Actor<SequencedMessage>) =
    let rec loop () = actor {
        let! sequencedMessage = mailbox.Receive ()
        printfn "SequencedMessageConsumer: received: %A" sequencedMessage
        return! loop ()
    }
    loop ()

let sequencedMessageConsumerRef = spawn system "sequencedMessageConsumer" sequencedMessageConsumer
let resequencerConsumerRef = spawn system "resequencerConsumer" <| resequencerConsumer sequencedMessageConsumerRef
let chaosRouterRef = spawn system "chaosRouter" <| chaosRouter resequencerConsumerRef

[1 .. 5] |> List.iter (fun index -> chaosRouterRef <! SequencedMessage("ABC", index, 5))
[1 .. 5] |> List.iter (fun index -> chaosRouterRef <! SequencedMessage("XYZ", index, 5))
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/Resequencer.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Composed Message Processor

```fsharp
// No code example
```

[Sections](#Sections)

##Scatter-Gather

```fsharp
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

```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/ScatterGather.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Routing Slip

```fsharp
type PostalAddress = { Address1: string; Address2: string; City: string; State: string; ZipCode: string }
type Telephone = Telephone of number: string
type CustomerInformation = { Name: string; FederalTaxId: string }
type ContactInformation = { PostalAddress: PostalAddress; Telephone: Telephone }
type ServiceOption = { Id: string; Description: string }
type RegistrationData = { CustomerInformation: CustomerInformation; ContactInformation: ContactInformation; ServiceOption: ServiceOption }
type ProcessStep = { Name: string; Processor: IActorRef }
type RegistrationProcess = { ProcessId: string; ProcessSteps: ProcessStep list; CurrentStep: int } with
    static member Create (processId, processSteps) = { ProcessId = processId; ProcessSteps = processSteps; CurrentStep = 0 }
    member this.IsCompleted with get () = this.CurrentStep >= this.ProcessSteps.Length
    member this.NextStep () = 
        if (this.IsCompleted) then failwith "Process had already completed."
        else this.ProcessSteps |> List.item this.CurrentStep
    member this.StepCompleted () = { this with CurrentStep = this.CurrentStep + 1 }
type RegisterCustomer = { RegistrationData: RegistrationData; RegistrationProcess: RegistrationProcess } with
    member this.Advance () = 
        let advancedProcess = this.RegistrationProcess.StepCompleted ()
        if not advancedProcess.IsCompleted then (advancedProcess.NextStep ()).Processor <! { this with RegistrationProcess = advancedProcess }
        else ()

let creditChecker (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let federalTaxId = registerCustomer.RegistrationData.CustomerInformation.FederalTaxId
        printfn "CreditChecker: handling register customer to perform credit check: %s" federalTaxId
        registerCustomer.Advance ()
    }
    loop ()

let contactKeeper (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let contactInfo = registerCustomer.RegistrationData.ContactInformation
        printfn "ContactKeeper: handling register customer to keep contact information: %A" contactInfo
        registerCustomer.Advance ()
    }
    loop ()

let customerVault (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let customerInformation = registerCustomer.RegistrationData.CustomerInformation
        printfn "CustomerVault: handling register customer to create a new custoner: %A" customerInformation
        registerCustomer.Advance ()
    }
    loop ()

let servicePlanner (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let serviceOption = registerCustomer.RegistrationData.ServiceOption
        printfn "ServicePlanner: handling register customer to plan a new customer service: %A" serviceOption
        registerCustomer.Advance ()
    }
    loop ()

module ServiceRegistry =
    let contactKeeper (system: ActorSystem) (id: string) = spawn system (sprintf "contactKeeper-%s" id) contactKeeper
    let creditChecker (system: ActorSystem) (id: string) = spawn system (sprintf "creditChecker-%s" id) creditChecker
    let customerVault (system: ActorSystem) (id: string) = spawn system (sprintf "customerVault-%s" id) customerVault
    let servicePlanner (system: ActorSystem) (id: string) = spawn system (sprintf "servicePlanner-%s" id) servicePlanner

let processId = (Guid.NewGuid ()).ToString ()
let step1 = { Name = "create_customer"; Processor = ServiceRegistry.customerVault system processId }
let step2 = { Name = "set_up_contact_info"; Processor = ServiceRegistry.contactKeeper system processId }
let step3 = { Name = "select_service_plan"; Processor = ServiceRegistry.servicePlanner system processId }
let step4 = { Name = "check_credit"; Processor = ServiceRegistry.creditChecker system processId }
let registrationProcess = RegistrationProcess.Create (processId, [ step1; step2; step3; step4 ])
let registrationData = { 
    CustomerInformation = { Name = "ABC, Inc."; FederalTaxId = "123-45-6789" }
    ContactInformation = { PostalAddress = { Address1 = "123 Main Street"; Address2 = "Suite 100"; City = "Boulder"; State = "CO"; ZipCode = "80301" }
                           Telephone = Telephone "303-555-1212" }
    ServiceOption = { Id = "99-1203"; Description = "A description of 99-1203." } }
let registerCustomer = { RegistrationData = registrationData; RegistrationProcess = registrationProcess }

(registrationProcess.NextStep ()).Processor <! registerCustomer
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/RoutingSlip.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Process Manager

```fsharp
type BankLoanRateQuote = { BankId: string; BankLoanRateQuoteId: string; InterestRate: decimal }
type LoanBrokerMessage = 
    | ProcessStarted of processId: string * ``process``: IActorRef
    | ProcessStopped of processId: string * ``process``: IActorRef
    | QuoteBestLoanRate of taxId: string * amount: int * termInMonths: int
    | BankLoanRateQuoted of bankId: string * bankLoanRateQuoteId: string * loadQuoteReferenceId: string * taxId: string * interestRate: decimal
    | CreditChecked of creditProcessingReferenceId: string * taxId: string * score: int
    | CreditScoreForLoanRateQuoteDenied of loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * score: int
    | CreditScoreForLoanRateQuoteEstablished of loanRateQuoteId: string * taxId: string * score: int * amount: int * termInMonths: int
    | LoanRateBestQuoteFilled of loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * creditScore: int * bestBankLoanRateQuote: BankLoanRateQuote
    | LoanRateQuoteRecorded of loanRateQuoteId: string * taxId: string * bankLoanRateQuote: BankLoanRateQuote
    | LoanRateQuoteStarted of loanRateQuoteId: string * taxId: string
    | LoanRateQuoteTerminated of loanRateQuoteId: string * taxId: string
type LoanRateQuoteMessage = 
    | StartLoanRateQuote of expectedLoanRateQuotes: int
    | EstablishCreditScoreForLoanRateQuote of loanRateQuoteId: string * taxId: string * score: int
    | RecordLoanRateQuote of bankId: string * bankLoanRateQuoteId: string * interestRate: decimal
    | TerminateLoanRateQuote
type QuoteLoanRate = QuoteLoanRate of loadQuoteReferenceId: string * taxId: string * creditScore: int * amount: int * termInMonths: int
type BestLoanRateDenied = BestLoanRateDenied of loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * creditScore: int
type BestLoanRateQuoted = BestLoanRateQuoted of bankId: string * loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * creditScore: int * interestRate: decimal
type CheckCredit = CheckCredit of creditProcessingReferenceId: string * taxId: string

module Process =
    let processOf processId processes = processes |> Map.find processId
    let startProcess processId ``process`` self processes =
        self <! ProcessStarted(processId, ``process``)
        processes |> Map.add processId ``process``
    let stopProcess processId self processes =
        let ``process`` = processOf processId processes
        self <! ProcessStopped(processId, ``process``)
        processes |> Map.remove processId

let loanRateQuote loanRateQuoteId taxId amount termInMonths loanBroker (mailbox: Actor<_>) =
    let rec loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore = actor {
        let quotableCreditScore score = score > 399

        let bestBankLoanRateQuote () = bankLoanRateQuotes |> List.minBy (fun bankLoanRateQuote -> bankLoanRateQuote.InterestRate)

        let! message = mailbox.Receive () 
        match message with
        | StartLoanRateQuote expectedLoanRateQuotes -> 
            loanBroker <! LoanRateQuoteStarted(loanRateQuoteId, taxId)
            return! loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore
        | EstablishCreditScoreForLoanRateQuote(loanRateQuoteId, taxId, creditRatingScore) -> 
            if quotableCreditScore creditRatingScore then loanBroker <! CreditScoreForLoanRateQuoteEstablished(loanRateQuoteId, taxId, creditRatingScore, amount, termInMonths)
            else loanBroker <! CreditScoreForLoanRateQuoteDenied(loanRateQuoteId, taxId, amount, termInMonths, creditRatingScore)
            return! loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore
        | RecordLoanRateQuote(bankId, bankLoanRateQuoteId, interestRate) -> 
            let bankLoanRateQuote = { BankId = bankId; BankLoanRateQuoteId = bankLoanRateQuoteId; InterestRate = interestRate }
            loanBroker <! LoanRateQuoteRecorded(loanRateQuoteId, taxId, bankLoanRateQuote)
            if bankLoanRateQuotes |> List.length >= expectedLoanRateQuotes then loanBroker <! LoanRateBestQuoteFilled(loanRateQuoteId, taxId, amount, termInMonths, creditRatingScore, bestBankLoanRateQuote ())
            else ()
            return! loop <| bankLoanRateQuotes @ [bankLoanRateQuote] <| expectedLoanRateQuotes <| creditRatingScore
        | TerminateLoanRateQuote -> 
            loanBroker <! LoanRateQuoteTerminated(loanRateQuoteId, taxId)
            return! loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore
    }
    loop [] 0 0

let loanBroker creditBureau banks (mailbox: Actor<_>) =
    let rec loop processes = actor {
        let! message = mailbox.Receive ()
        match message with
        | BankLoanRateQuoted(bankId, bankLoanRateQuoteId, loadQuoteReferenceId, taxId, interestRate) ->
            printfn "%A" message
            Process.processOf loadQuoteReferenceId processes <! RecordLoanRateQuote(bankId, bankLoanRateQuoteId, interestRate)
            return! loop processes
        | CreditChecked(creditProcessingReferenceId, taxId, score) ->
            printfn "%A" message
            Process.processOf creditProcessingReferenceId processes <! EstablishCreditScoreForLoanRateQuote(creditProcessingReferenceId, taxId, score)
            return! loop processes
        | CreditScoreForLoanRateQuoteDenied(loanRateQuoteId, taxId, amount, termInMonths, score) ->
            printfn "%A" message
            Process.processOf loanRateQuoteId processes <! TerminateLoanRateQuote
            let denied = BestLoanRateDenied(loanRateQuoteId, taxId, amount, termInMonths, score)
            printfn "Would be sent to original requester: %A" denied
            return! loop processes
        | CreditScoreForLoanRateQuoteEstablished(loanRateQuoteId, taxId, score, amount, termInMonths) ->
            printfn "%A" message
            banks |> List.iter (fun bank -> bank <! QuoteLoanRate(loanRateQuoteId, taxId, score, amount, termInMonths))
            return! loop processes
        | LoanRateBestQuoteFilled(loanRateQuoteId, taxId, amount, termInMonths, creditScore, bestBankLoanRateQuote) ->
            printfn "%A" message
            let best = BestLoanRateQuoted(bestBankLoanRateQuote.BankId, loanRateQuoteId, taxId, amount, termInMonths, creditScore, bestBankLoanRateQuote.InterestRate)
            printfn "Would be sent to original requester: %A" best
            return! loop <| Process.stopProcess loanRateQuoteId mailbox.Self processes 
        | LoanRateQuoteRecorded(loanRateQuoteId, taxId, bankLoanRateQuote) ->
            printfn "%A" message
            return! loop processes
        | LoanRateQuoteStarted(loanRateQuoteId, taxId) ->
            printfn "%A" message
            creditBureau <! CheckCredit(loanRateQuoteId, taxId)
            return! loop processes
        | LoanRateQuoteTerminated(loanRateQuoteId, taxId) ->
            printfn "%A" message
            return! loop <| Process.stopProcess loanRateQuoteId mailbox.Self processes 
        | ProcessStarted(processId, ``process``) ->
            printfn "%A" message
            ``process`` <! StartLoanRateQuote(banks.Length)
            return! loop processes
        | ProcessStopped (_,_) -> printfn "%A" message
        | QuoteBestLoanRate(taxId, amount, termInMonths) -> 
            let loanRateQuoteId = (Guid.NewGuid ()).ToString () // LoanRateQuote.id
            let loanRateQuote = spawn mailbox.Context "loanRateQuote" <| loanRateQuote loanRateQuoteId taxId amount termInMonths mailbox.Self
            return! loop <| Process.startProcess loanRateQuoteId loanRateQuote mailbox.Self processes
    }
    loop Map.empty

let creditBureau (mailbox: Actor<_>) =
    let creditRanges = [300; 400; 500; 600; 700]
    let randomCreditRangeGenerator = Random()
    let randomCreditScoreGenerator = Random()
    
    let rec loop () = actor {
        let! (CheckCredit(creditProcessingReferenceId, taxId)) = mailbox.Receive ()
        let range = creditRanges |> List.item (randomCreditRangeGenerator.Next 5)
        let score = range + randomCreditScoreGenerator.Next 20
        mailbox.Sender () <! CreditChecked(creditProcessingReferenceId, taxId, score)        
        return! loop ()
    }
    loop ()

let bank bankId primeRate ratePremium (mailbox: Actor<_>) =
    let randomDiscount = Random()
    let randomQuoteId = Random()
    let calculateInterestRate amount months creditScore = 
        let creditScoreDiscount = creditScore / 100.0m / 10.0m - ((randomDiscount.Next 5 |> decimal) * 0.05m)
        primeRate + ratePremium + ((months / 12.0m) / 10.0m) - creditScoreDiscount
    
    let rec loop () = actor {
        let! (QuoteLoanRate(loadQuoteReferenceId, taxId, creditScore, amount, termInMonths)) = mailbox.Receive ()
        let interestRate = calculateInterestRate amount (decimal termInMonths) (decimal creditScore)
        mailbox.Sender () <! BankLoanRateQuoted(bankId, (randomQuoteId.Next 1000).ToString (), loadQuoteReferenceId, taxId, interestRate)
        return! loop ()
    }
    loop ()

let creditBureauRef = spawn system "creditBureau" creditBureau
let bank1Ref = spawn system "bank1" <| bank "bank1" 2.75m 0.30m
let bank2Ref = spawn system "bank2" <| bank "bank2" 2.73m 0.31m
let bank3Ref = spawn system "bank3" <| bank "bank3" 2.80m 0.29m
let loanBrokerRef = spawn system "loanBroker" <| loanBroker creditBureauRef [bank1Ref; bank2Ref; bank3Ref]

loanBrokerRef <! QuoteBestLoanRate("111-11-1111", 100000, 84)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageRouting/ProcessManager.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Message Broker

```fsharp
// No code example
```

[Sections](#Sections)
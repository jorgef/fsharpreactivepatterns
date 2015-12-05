#load "..\References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
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
#load "../References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

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
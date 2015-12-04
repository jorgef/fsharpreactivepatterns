#load "..\References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type RabbitMQTextMessage = RabbitMQTextMessage of string

let inventoryProductAllocationBridge (mailbox: Actor<_>) =
    let translatedToInventoryProductAlloction = sprintf "Inventory product alloction for %s"
    let acknowledgeDelivery (RabbitMQTextMessage textMessage) = printfn "InventoryProductAllocationBridge: acknowledged '%s'" textMessage

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let (RabbitMQTextMessage textMessage) = message
        printfn "InventoryProductAllocationBridge: received '%s'" textMessage
        let inventoryProductAllocation = translatedToInventoryProductAlloction textMessage
        printfn "InventoryProductAllocationBridge: translated '%s'" inventoryProductAllocation
        acknowledgeDelivery message
        return! loop ()
    }
    loop ()

let inventoryProductAllocationBridgeRef = spawn system "inventoryProductAllocationBridge" inventoryProductAllocationBridge

inventoryProductAllocationBridgeRef <! RabbitMQTextMessage "Rabbit test message"
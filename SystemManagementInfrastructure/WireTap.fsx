#load "../References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Order = Order
type ProcessOrder = ProcessOrder of order: Order

let orderProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "OrderProcessor: %A" message
        return! loop ()
    }
    loop ()

let messageLogger next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageLogger: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let orderProcessorRef = spawn system "orderProcessor" orderProcessor
let loggerRef = spawn system "logger" <| messageLogger orderProcessorRef

let orderProcessorWireTap = loggerRef

orderProcessorWireTap <! ProcessOrder(Order)
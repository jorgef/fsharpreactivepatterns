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

let messageDebugger next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageDebugger: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let messageTester next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageTester: %A" message
        next <! message
        return! loop ()
    }
    loop ()

let messageValidator next (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "MessageValidator: %A" message
        next <! message
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
let debuggerRef = spawn system "debugger" <| messageDebugger orderProcessorRef
let testerRef = spawn system "tester" <| messageTester debuggerRef
let validatorRef = spawn system "validator" <| messageValidator testerRef
let loggerRef = spawn system "logger" <| messageLogger validatorRef

let orderProcessorDetour = loggerRef // change to = orderProcessorRef to turn the Detour off

orderProcessorDetour <! ProcessOrder(Order)
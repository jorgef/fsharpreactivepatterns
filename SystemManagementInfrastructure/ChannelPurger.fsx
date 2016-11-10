#load "../References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Message =
    | ProcessOrder
    | PurgeNow
    | StopPurge

let orderProcessor (mailbox: Actor<_>) =
    let rec normal () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ProcessOrder -> 
            printfn "Normal: %A" message
            return! normal ()
        | PurgeNow -> 
            printfn "Normal: %A" message
            return! purger ()
        | _ -> return! normal () }
    and purger () = actor {
        let! message = mailbox.Receive ()
        match message with
        | StopPurge -> 
            printfn "Purger: %A" message
            return! normal ()
        | _ -> return! purger ()
    }
    normal ()

let orderProcessorRef = spawn system "orderProcessor" orderProcessor

orderProcessorRef <! ProcessOrder
orderProcessorRef <! PurgeNow
orderProcessorRef <! StopPurge
orderProcessorRef <! ProcessOrder
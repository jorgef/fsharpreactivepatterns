#load "..\References.fsx"

open Akka.FSharp
open Akka.Actor

let system = System.create "system" <| Configuration.load ()

let alternatingRouter (processor1: IActorRef) (processor2: IActorRef) (mailbox: Actor<_>) =
    let rec loop alternate = actor {
        let alternateProcessor () = if alternate = 1 then processor1, 2 else processor2, 1
        let! message = mailbox.Receive ()
        let processor, nextAlternate = alternateProcessor ()
        printfn "AlternatingRouter: routing %O to %s" message processor.Path.Name
        processor <! message
        return! loop nextAlternate
    }
    loop 1

let processor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Processor: %s received %O"  message mailbox.Self.Path.Name
        return! loop ()
    }
    loop ()

let processor1Ref = spawn system "processor1" processor
let processor2Ref = spawn system "processor2" processor
let alternatingRouterRef = spawn system "alternatingRouter" <| alternatingRouter processor1Ref processor2Ref

[1..10] |> List.iter (fun i -> alternatingRouterRef <! sprintf "Message #%i" i)
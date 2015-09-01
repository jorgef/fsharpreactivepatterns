#load "../References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type ProcessorMessage = ProcessJob of int * int * int

let processorActor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! ProcessJob(x,y,z) = mailbox.Receive ()
        printfn "Received ProcessJob %i %i %i" x y z
        return! loop ()
    }
    loop ()

let processor = spawn system "processor" processorActor

processor <! ProcessJob(1, 3, 5)
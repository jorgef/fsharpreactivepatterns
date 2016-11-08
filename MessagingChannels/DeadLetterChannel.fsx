#load "../References.fsx"

open Akka.Event
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

let sysListener (mailbox: Actor<DeadLetter>) = 
    let rec loop () = actor {
        let! deadLetter = mailbox.Receive ()
        printfn "SysListner, DeadLetter received: %A" deadLetter.Message
        return! loop ()
    }
    loop ()

let sysListenerRef = spawn system "sysListener" sysListener
subscribe typeof<DeadLetter> sysListenerRef system.EventStream

let deadActorRef = select "akka://system/user/deadActor" system
deadActorRef <! "Message to dead actor"
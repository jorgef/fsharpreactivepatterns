#load "..\References.fsx"

open System
open Akka.Actor
open Akka.FSharp
open Newtonsoft.Json

let system = System.create "system" <| Configuration.load ()

let caller orderQueryService (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Caller: result received: %A" message
        return! loop ()
    }
    loop ()

let caller orderQueryService (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Caller: result received: %A" message
        return! loop ()
    }
    loop ()
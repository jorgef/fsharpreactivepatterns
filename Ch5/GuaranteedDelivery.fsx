#load "..\References.fsx"

open Akka.Actor
open Akka.FSharp
open Akka.Persistence.FSharp

let system = System.create "system" <| Configuration.load ()

let loanBroker (mailbox: Actor<_>) =
    let rec loop () = actor {
        return! loop ()
    }
    loop ()

// TDB: Use spawnPersist when is ready (@horusiath is working on it)
// https://github.com/Horusiath/Akkling/blob/actor-expression-2.0/examples/persistence.fsx
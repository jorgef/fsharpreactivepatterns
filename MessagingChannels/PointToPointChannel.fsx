#load "../References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

let assertion v = if not v then printfn "Assertion Failed" else () // F# assert function works only in debug mode (fsi --define:DEBUG)

let actorA actorB (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    actorB <! "Hello, from actor A!"
    actorB <! "Hello again, from actor A!"
    loop ()

let actorB (mailbox: Actor<string>) =
    let rec loop hello helloAgain goodbye goodbyeAgain = actor {
        let! message = mailbox.Receive ()
        let hello = hello + (if message.Contains "Hello" then 1 else 0)
        let helloAgain = helloAgain + (if message.Contains "Hello again" then 1 else 0)
        assertion (hello = 0 || hello > helloAgain)
        let goodbye = goodbye + (if message.Contains "Goodbye" then 1 else 0)
        let goodbyeAgain = goodbyeAgain + (if message.Contains "Goodbye again" then 1 else 0)
        assertion (goodbye = 0 || goodbye > goodbyeAgain)
        printfn "ActorB: received %s" message
        return! loop hello helloAgain goodbye goodbyeAgain
    }
    loop 0 0 0 0 

let actorC actorB (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    actorB <! "Goodbye, from actor C!"
    actorB <! "Goodbye again, from actor C!"
    loop ()

let actorBRef = spawn system "actorB" actorB
let actorARef = spawn system "actorA" <| actorA actorBRef
let actorCRef = spawn system "actorC" <| actorC actorBRef
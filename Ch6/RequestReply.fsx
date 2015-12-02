#load "..\References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type ServerMessage = Request of string
type ClientMessage =
    | Reply of string
    | StartWith of IActorRef

let client (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | StartWith server ->
            printfn "Client: is starting..."
            server <! Request "REQ-1"
        | Reply what ->
            printfn "Client: received response: %s" what
        return! loop ()
    }
    loop ()

let server (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! Request what = mailbox.Receive ()
        printfn "Server: received request value: %s" what
        mailbox.Sender () <! Reply (sprintf "RESP-1 for %s" what)
        return! loop ()
    }
    loop ()

let clientRef = spawn system "client" client
let serverRef = spawn system "server" server

clientRef <! StartWith serverRef
#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type ServerMessage = 
    | Request of string
    | RequestComplex of string
type ClientMessage =
    | Reply of string
    | StartWith of IActorRef
    | ReplyToComplex of string
type WorkerMessage =
    | WorkerRequestComplex of string

let client (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | StartWith server ->
            printfn "Client: is starting..."
            server <! Request "REQ-1"
            server <! RequestComplex "REQ-20"
        | Reply what -> printfn "Client: received response: %s" what
        | ReplyToComplex what -> printfn "Client: received reply to complex: %s" what
        return! loop ()
    }
    loop ()

let worker (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! WorkerRequestComplex what = mailbox.Receive ()
        printfn "Worker: received complex request value: %s" what
        mailbox.Sender () <! ReplyToComplex (sprintf "RESP-2000 for %s" what)
        return! loop ()
    }
    loop ()

let server (mailbox: Actor<_>) =
    let workerRef = spawn mailbox.Context "worker" worker
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | Request what -> 
            printfn "Server: received request value: %s" what
            mailbox.Sender () <! Reply (sprintf "RESP-1 for %s" what)
        | RequestComplex what -> 
            printfn "Server: received request value: %s" what
            mailbox.Sender () <! Reply (sprintf "RESP-1 for %s" what)
            workerRef.Forward <| WorkerRequestComplex what
        return! loop ()
    }
    loop ()

let clientRef = spawn system "client" client
let serverRef = spawn system "server" server

clientRef <! StartWith serverRef
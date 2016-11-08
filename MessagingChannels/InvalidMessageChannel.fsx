#load "../References.fsx"

open System
open System.Text
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type InvalidMessage<'a> = { Sender: IActorRef; Receiver: IActorRef; Message: 'a }
type ProcessIncomingOrder = ProcessIncomingOrder of byte array

let invalidMessageChannel (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! { Sender = s; Receiver = r; Message = m } = mailbox.Receive ()
        printfn "InvalidMessageChannel: InvalidMessage received, message: %A" m
        return! loop ()
    }
    loop ()

let authenticator (nextFilter: IActorRef) (invalidMessageChannel: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? ProcessIncomingOrder as message ->
            let (ProcessIncomingOrder(bytes)) = message 
            let text = Encoding.Default.GetString bytes
            printfn "Decrypter: processing %s" text
            let orderText = text.Replace ("(encryption)", String.Empty)
            nextFilter <! ProcessIncomingOrder(Encoding.Default.GetBytes orderText)
        | invalid -> invalidMessageChannel <! { Sender = mailbox.Sender (); Receiver = mailbox.Self; Message = invalid }
        return! loop () 
    }
    loop ()

let nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        return! loop ()
    }
    loop ()

let invalidMessageChannelRef = spawn system "invalidMessageChannel" invalidMessageChannel
let nextFilterRef = spawn system "nextFilter" nextFilter
let authenticatorRef = spawn system "authenticator" <| authenticator nextFilterRef invalidMessageChannelRef

authenticatorRef <! "Invalid message"
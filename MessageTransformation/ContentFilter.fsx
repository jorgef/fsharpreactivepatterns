#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Message =
    | FilteredMessage of light: string * ``and``: string * fluffy: string * message: string
    | UnfilteredPayload of largePayload: string

let messageContentFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | UnfilteredPayload payload -> 
            printfn "MessageContentFilter: received unfiltered message: %s" payload
            mailbox.Sender () <! FilteredMessage("this", "feels", "so", "right")
        | FilteredMessage _ -> printfn "MessageContentFilter: unexpected"
        return! loop ()
    }
    loop ()

let messageExchangeDispatcher (mailbox: Actor<_>) =
    let messageContentFilter = spawn mailbox.Context "messageContentFilter" messageContentFilter
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | UnfilteredPayload payload ->
            printfn "MessageExchangeDispatcher: received unfiltered message: %s" payload
            messageContentFilter <! message
        | FilteredMessage _ -> printfn "MessageExchangeDispatcher: dispatching: %A" message
        return! loop ()
    }
    loop ()

let messageExchangeDispatcherRef = spawn system "messageExchangeDispatcher" messageExchangeDispatcher

messageExchangeDispatcherRef <! UnfilteredPayload "A very large message with complex structure..."
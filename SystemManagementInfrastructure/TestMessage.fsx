#load "../References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Message = { IsTest: bool }

let inline (|TestMessage|_|) (message: 'a) = 
    let inline isTest (message: ^a) = (^a: (member get_IsTest: unit -> bool) (message))
    if (isTest message) then Some message else None

let processor (mailbox: Actor<Message>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | TestMessage message -> printfn "Test message: %A" message
        | message -> printfn "Production message: %A" message
        return! loop ()
    }
    loop ()

let processorRef = spawn system "processor" processor

processorRef <! { IsTest = true }
processorRef <! { IsTest = false }
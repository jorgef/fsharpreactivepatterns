#load "..\References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type IReplyToSupport =
    abstract member Reply: obj -> unit
    abstract member SetUpReplyToSupport: string -> unit

type IRegisterCustomer = 
    inherit IReplyToSupport
    abstract member Message: string with get

type RabbitMQReplyToSupport() =
    let mutable returnAddress = String.Empty
    interface IReplyToSupport with
        member this.Reply message = printfn "RabbitMQReplyToSupport: Replying %A to \"%s\"" message returnAddress
        member this.SetUpReplyToSupport replyReturnAddress = returnAddress <- replyReturnAddress

type RegisterCustomerRabbitMQReplyToMapEnvelope(mapMessage: Map<string, string>) as this =
    inherit RabbitMQReplyToSupport()
    let this = this :> IReplyToSupport
    do this.SetUpReplyToSupport(mapMessage |> Map.find "returnAddress")
    let message = mapMessage |> Map.find "message"
    interface IRegisterCustomer with
        member this.Message with get () = message

let customerRegistrar (mailbox: Actor<IRegisterCustomer>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        printfn "CustomerRegistrar: Received \"%s\"" registerCustomer.Message
        registerCustomer.Reply "hi"
        return! loop ()
    }
    loop ()

let receivedMessageAsMap _ = [ ("returnAddress", "http://caller/"); ("message", "hello") ] |> Map.ofList
let wireMessage = ()
let mapMessage = receivedMessageAsMap wireMessage

let customerRegistrarRef = spawn system "customerRegistrar" customerRegistrar

let registerCustomer = RegisterCustomerRabbitMQReplyToMapEnvelope(mapMessage)
customerRegistrarRef <! registerCustomer
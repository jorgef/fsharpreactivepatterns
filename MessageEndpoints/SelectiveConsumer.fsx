#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type MessageTypeA = MessageTypeA
type MessageTypeB = MessageTypeB
type MessageTypeC = MessageTypeC

let selectiveConsumer (consumerOfA: IActorRef) (consumerOfB: IActorRef) (consumerOfC: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? MessageTypeA -> 
            consumerOfA.Forward message
            return! loop ()
        | :? MessageTypeB -> 
            consumerOfB.Forward message
            return! loop ()
        | :? MessageTypeC -> 
            consumerOfC.Forward message
            return! loop ()
        | _ -> return! loop ()
    }
    loop ()

let consumerOfMessageTypeA (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "ConsumerOfMessageTypeA: %A" message
        return! loop ()
    }
    loop ()

let consumerOfMessageTypeB (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "ConsumerOfMessageTypeB: %A" message
        return! loop ()
    }
    loop ()

let consumerOfMessageTypeC (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "ConsumerOfMessageTypeC: %A" message
        return! loop ()
    }
    loop ()

let consumerOfARef = spawn system "consumerOfA" consumerOfMessageTypeA
let consumerOfBRef = spawn system "consumerOfB" consumerOfMessageTypeB
let consumerOfCRef = spawn system "consumerOfC" consumerOfMessageTypeC
let selectiveConsumerRef = spawn system "selectiveConsumer" <| selectiveConsumer consumerOfARef consumerOfBRef consumerOfCRef

selectiveConsumerRef <! MessageTypeA
selectiveConsumerRef <! MessageTypeB
selectiveConsumerRef <! MessageTypeC
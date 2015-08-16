#r @"packages\Akka.1.0.4\lib\net45\Akka.dll"
#r @"packages\Akka.FSharp.1.0.4\lib\net45\Akka.FSharp.dll"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

let messageChannel (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "%s" message
        return! loop ()
    }
    loop ()

let messageChannelActor = spawn system "message-channel" messageChannel

messageChannelActor <! "hello"
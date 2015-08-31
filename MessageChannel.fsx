#r @"packages\Newtonsoft.Json.7.0.1\lib\net45\Newtonsoft.Json.dll"
#r @"packages\FsPickler.1.2.21\lib\net45\FsPickler.dll"
#r @"packages\FSPowerPack.Linq.Community.3.0.0.0\Lib\Net40\FSharp.PowerPack.Linq.dll"
#r @"packages\FSPowerPack.Core.Community.3.0.0.0\Lib\Net40\FSharp.PowerPack.dll"
#r @"packages\Akka.1.0.4\lib\net45\Akka.dll"
#r @"packages\Akka.FSharp.1.0.4\lib\net45\Akka.FSharp.dll"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

let messageChannel (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Received: %s" message
        return! loop ()
    }
    loop ()

let messageChannelActor = spawn system "message-channel" messageChannel

messageChannelActor <! "hello"
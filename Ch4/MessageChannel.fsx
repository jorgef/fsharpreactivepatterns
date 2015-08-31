#r @"..\packages\Newtonsoft.Json.7.0.1\lib\net45\Newtonsoft.Json.dll"
#r @"..\packages\FsPickler.1.2.21\lib\net45\FsPickler.dll"
#r @"..\packages\FSPowerPack.Linq.Community.3.0.0.0\Lib\Net40\FSharp.PowerPack.Linq.dll"
#r @"..\packages\FSPowerPack.Core.Community.3.0.0.0\Lib\Net40\FSharp.PowerPack.dll"
#r @"..\packages\Akka.1.0.4\lib\net45\Akka.dll"
#r @"..\packages\Akka.FSharp.1.0.4\lib\net45\Akka.FSharp.dll"

open Akka.FSharp

type ProcessorMessage = ProcessJob of int * int * int

let system = System.create "system" <| Configuration.load ()

let processorActor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! ProcessJob(x,y,z) = mailbox.Receive ()
        printfn "Received: %i %i %i" x y z
        return! loop ()
    }
    loop ()

let processor = spawn system "processor" processorActor

processor <! ProcessJob(1, 3, 5)
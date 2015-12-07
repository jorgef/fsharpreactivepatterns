#load "..\References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type WorkItem = { Name: string }

let workConsumer (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! workItem = mailbox.Receive ()
        printfn "%s for: %s" mailbox.Self.Path.Name workItem.Name
        return! loop ()
    }
    loop ()

let workItemsProviderRef = spawnOpt system "workItemsProvider" workConsumer [ Router(Akka.Routing.SmallestMailboxPool(5)) ]

[ 1 .. 100 ]
|> List.iter (fun itemCount -> workItemsProviderRef <! { Name = "WorkItem" + itemCount.ToString () })

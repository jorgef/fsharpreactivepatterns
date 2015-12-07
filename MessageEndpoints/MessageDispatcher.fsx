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

let workItemsProvider = spawnOpt system "workItemsProvider" workConsumer [ Router(Akka.Routing.RoundRobinPool(5)) ]

workItemsProvider <! { Name = "WorkItem1" }
workItemsProvider <! { Name = "WorkItem2" }
workItemsProvider <! { Name = "WorkItem3" }
workItemsProvider <! { Name = "WorkItem4" }
workItemsProvider <! { Name = "WorkItem5" }
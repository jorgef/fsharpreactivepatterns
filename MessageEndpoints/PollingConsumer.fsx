#load "../References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type WorkItem = { Name: string }
type WorkConsumerMessage = 
    | WorkNeeded
    | WorkItemsAllocated of workItems: WorkItem list
    | WorkOnItem of workItem: WorkItem
type WorkItemsProvider = 
    | AllocateWorkItems of numberOfItems: int

let workItemsProvider (mailbox: Actor<_>) =
    let rec loop workItemsNamed = actor {
        let allocateWorkItems numberOfItems =
            let allocatedWorkItems = 
                [ 1 .. numberOfItems ]
                |> List.map (fun itemCount -> 
                    let nameIndex = workItemsNamed + itemCount
                    { Name = "WorkItem" + nameIndex.ToString () })
            allocatedWorkItems, workItemsNamed + numberOfItems

        let! AllocateWorkItems numberOfItems = mailbox.Receive ()
        let allocatedWorkItems, workItemsNamed = allocateWorkItems(numberOfItems)
        mailbox.Sender () <! WorkItemsAllocated allocatedWorkItems
        return! loop workItemsNamed
    }
    loop 0

let workConsumer workItemsProvider (mailbox: Actor<_>) =
    mailbox.Defer (fun () -> mailbox.Context.Stop(workItemsProvider))
    let rec loop totalItemsWorkedOn = actor {
        let performWorkOn workItem =
            let totalItemsWorkedOn = totalItemsWorkedOn + 1
            if (totalItemsWorkedOn >= 15) then  mailbox.Context.Stop mailbox.Self else ()
            totalItemsWorkedOn

        let! message = mailbox.Receive ()
        match message with
        | WorkItemsAllocated workitems ->
            printfn "WorkItemsAllocated..."
            workitems |> List.iter (fun workItem -> mailbox.Self <! WorkOnItem(workItem))
            mailbox.Self <! WorkNeeded
            return! loop totalItemsWorkedOn
        | WorkNeeded ->
            printfn "WoorkNeeded..."
            workItemsProvider <! AllocateWorkItems 5
            return! loop totalItemsWorkedOn
        | WorkOnItem workItem ->
            printfn "Performed work on: %s" workItem.Name
            let totalItemsWorkedOn = performWorkOn workItem
            return! loop totalItemsWorkedOn
    }
    loop 0

let workItemsProviderRef = spawn system "workItemsProvider" workItemsProvider
let workConsumerRef = spawn system "workConsumer" <| workConsumer workItemsProviderRef

workConsumerRef <! WorkNeeded
#load "..\References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type SequencedMessage =  SequencedMessage of correlationId: string * index: int * total: int
type ResequencedMessages = { DispatchableIndex: int; SequencedMessages: SequencedMessage [] } with
    member this.AdvancedTo(dispatchableIndex: int) = { this with DispatchableIndex = dispatchableIndex }

let chaosRouter consumer (mailbox: Actor<_>) =
    let random = Random()
    let rec loop () = actor {
        let! SequencedMessage(correlationId, index, total) as sequencedMessage = mailbox.Receive ()
        let millis = random.Next 100
        printfn "ChaosRouter: delaying delivery of %A for %i milliseconds" sequencedMessage millis
        let duration = TimeSpan.FromMilliseconds (float millis)
        mailbox.Context.System.Scheduler.ScheduleTellOnce (duration, consumer, sequencedMessage)
        return! loop ()
    }
    loop ()

let resequencerConsumer actualConsumer (mailbox: Actor<_>) =
    let rec loop resequenced = actor {
        let dummySequencedMessages count = [| for _ in 1 .. count -> SequencedMessage("", -1, count) |]
        
        let resequence sequencedMessage resequenced = 
            let (SequencedMessage(correlationId, index, total)) = sequencedMessage
            let resequenced = 
                resequenced
                |> Map.tryFind correlationId
                |> Option.fold 
                    (fun _ v -> resequenced) 
                    (resequenced |> Map.add correlationId ( { DispatchableIndex = 1; SequencedMessages = dummySequencedMessages total }))
            resequenced
            |> Map.find correlationId
            |> fun m -> m.SequencedMessages.[index - 1] <- sequencedMessage
            resequenced

        let rec dispatchAllSequenced correlationId resequenced =
            let resequencedMessage = resequenced |> Map.find correlationId
            let dispatchableIndex = 
                resequencedMessage.SequencedMessages 
                |> Array.filter (fun (SequencedMessage(_, index, _) as sequencedMessage) -> index = resequencedMessage.DispatchableIndex)
                |> Array.fold (fun dispatchableIndex sequencedMessage -> 
                        actualConsumer <! sequencedMessage
                        dispatchableIndex + 1) 
                        resequencedMessage.DispatchableIndex
            let resequenced = resequenced |> Map.add correlationId (resequencedMessage.AdvancedTo dispatchableIndex)
            if resequencedMessage.SequencedMessages |> Array.exists (fun (SequencedMessage(_, index, _)) -> index = dispatchableIndex) then dispatchAllSequenced correlationId resequenced
            else resequenced

        let removeCompleted correlationId resequenced = 
            resequenced
            |> Map.find correlationId
            |> fun resequencedMessages -> 
                let (SequencedMessage(_,_,total)) = resequencedMessages.SequencedMessages.[0]
                if resequencedMessages.DispatchableIndex > total then
                    printfn "ResequencerConsumer: removed completed: %s" correlationId
                    resequenced |> Map.remove correlationId
                else resequenced

        let! SequencedMessage(correlationId, index, total) as unsequencedMessage = mailbox.Receive ()
        printfn "ResequencerConsumer: received: %A" unsequencedMessage
        let resequenced = 
            resequenced
            |> resequence unsequencedMessage
            |> dispatchAllSequenced correlationId
            |> removeCompleted correlationId
        return! loop resequenced
    }
    loop Map.empty

let sequencedMessageConsumer (mailbox: Actor<SequencedMessage>) =
    let rec loop () = actor {
        let! sequencedMessage = mailbox.Receive ()
        printfn "SequencedMessageConsumer: received: %A" sequencedMessage
        return! loop ()
    }
    loop ()

let sequencedMessageConsumerRef = spawn system "sequencedMessageConsumer" sequencedMessageConsumer
let resequencerConsumerRef = spawn system "resequencerConsumer" <| resequencerConsumer sequencedMessageConsumerRef
let chaosRouterRef = spawn system "chaosRouter" <| chaosRouter resequencerConsumerRef

[1 .. 5] |> List.iter (fun index -> chaosRouterRef <! SequencedMessage("ABC", index, 5))
[1 .. 5] |> List.iter (fun index -> chaosRouterRef <! SequencedMessage("XYZ", index, 5))
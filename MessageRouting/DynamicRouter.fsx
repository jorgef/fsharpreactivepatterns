#load "../References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Registration = 
    | InterestedIn of string
    | NoLongerInterestedIn of string
type TypeAMessage = TypeAMessage of string
type TypeBMessage = TypeBMessage of string
type TypeCMessage = TypeCMessage of string
type TypeDMessage = TypeDMessage of string

let typeAInterested interestRouter (mailbox: Actor<TypeAMessage>) =
    interestRouter <! InterestedIn(typeof<TypeAMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeAInterested: received: %A" message
        return! loop ()
    }
    loop ()

let typeBInterested interestRouter (mailbox: Actor<TypeBMessage>) =
    interestRouter <! InterestedIn(typeof<TypeBMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeBInterested: received: %A" message
        return! loop ()
    }
    loop ()

let typeCInterested interestRouter (mailbox: Actor<TypeCMessage>) =
    interestRouter <! InterestedIn(typeof<TypeCMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeCInterested: received: %A" message
        interestRouter <! NoLongerInterestedIn(typeof<TypeCMessage>.Name)
        return! loop ()
    }
    loop ()

let typeCAlsoInterested interestRouter (mailbox: Actor<TypeCMessage>) =
    interestRouter <! InterestedIn(typeof<TypeCMessage>.Name)

    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TypeCAlsoInterested: received: %A" message
        interestRouter <! NoLongerInterestedIn(typeof<TypeCMessage>.Name)
        return! loop ()
    }
    loop ()

let dunnoInterested (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "DunnoInterest: received undeliverable message: %A" message
        return! loop ()
    }
    loop ()

let typedMessageInterestRouter dunnoInterested (mailbox: Actor<_>) =
    let rec loop (interestRegistry: Map<string, IActorRef>) (secondaryInterestRegistry: Map<string, IActorRef>) = actor {
        let registerInterest messageType interested =
            interestRegistry
            |> Map.tryFind messageType
            |> Option.fold 
                (fun _ _ -> interestRegistry, Map.add messageType interested secondaryInterestRegistry) 
                (Map.add messageType interested interestRegistry, secondaryInterestRegistry)

        let unregisterInterest messageType wasInterested =
            interestRegistry
            |> Map.tryFind messageType
            |> Option.bind (fun i -> if i = wasInterested then Some (Map.remove messageType interestRegistry, secondaryInterestRegistry) else None)
            |> Option.bind (fun (p,s) -> s |> Map.tryFind messageType |> Option.map(fun i -> Map.add messageType i p,  Map.remove messageType secondaryInterestRegistry))
            |> Option.fold (fun _ i -> i) (interestRegistry, secondaryInterestRegistry)

        let sendFor message =
            let messageType = (message.GetType ()).Name
            interestRegistry
            |> Map.tryFind messageType
            |> Option.fold (fun _ i -> i) dunnoInterested
            |> fun i -> i <! message 

        let! message = mailbox.Receive ()
        match box message with
        | :? Registration as r ->
            match r with
            | InterestedIn messageType -> return! loop <|| registerInterest messageType (mailbox.Sender ())
            | NoLongerInterestedIn messageType -> return! loop <|| unregisterInterest messageType (mailbox.Sender ())
        | message -> 
            sendFor message
            return! loop interestRegistry secondaryInterestRegistry
    }
    loop Map.empty Map.empty

let dunnoInterestedRef = spawn system "dunnoInterested" dunnoInterested
let typedMessageInterestRouterRef = spawn system "typedMessageInterestRouter" <| typedMessageInterestRouter dunnoInterestedRef
let typeAInterestedRef = spawn system "typeAInterest" <| typeAInterested typedMessageInterestRouterRef
let typeBInterestedRef = spawn system "typeBInterest" <| typeBInterested typedMessageInterestRouterRef
let typeCInterestedRef = spawn system "typeCInterest" <| typeCInterested typedMessageInterestRouterRef
let typeCAlsoInterestedRef = spawn system "typeCAlsoInterested" <| typeCAlsoInterested typedMessageInterestRouterRef

typedMessageInterestRouterRef <! TypeAMessage("Message of TypeA.")
typedMessageInterestRouterRef <! TypeBMessage("Message of TypeB.")
typedMessageInterestRouterRef <! TypeCMessage("Message of TypeC.")
typedMessageInterestRouterRef <! TypeCMessage("Another message of TypeC.")
typedMessageInterestRouterRef <! TypeDMessage("Message of TypeD.")
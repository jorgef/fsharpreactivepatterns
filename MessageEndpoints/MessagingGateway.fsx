#load "../References.fsx"

open System
open Akka.Actor
open Akka.FSharp

type AggregateType = AggregateType of cacheActor: IActorRef
type RegisterAggregateId = RegisterAggregateId of id: string
type OrderMessage = 
    | InitializeOrder of amount: decimal
    | ProcessOrder
type CacheMessage = CacheMessage of id: string * actualMessage: obj * sender: IActorRef
type AggregateRef(id, cache) =
    interface ICanTell with
        member this.Tell (message: obj, sender: IActorRef) = cache <! CacheMessage(id, message, sender)

let order (mailbox: Actor<_>) =
    let rec loop amount = actor {
        let! message = mailbox.Receive ()
        match message with
        | InitializeOrder amount ->
            printfn "Initializing Order with %M" amount
            return! loop amount
        | ProcessOrder ->
            printfn "Processing Order is %A" message
            return! loop amount
    }
    loop 0m

let aggregateCache aggregateFunc (mailbox: Actor<_>) =
    let rec loop aggregateIds = actor {
        let! CacheMessage(id, actualMessage, sender) = mailbox.Receive ()
        let child = mailbox.Context.Child id
        let aggregate = if child = (ActorRefs.Nobody :> IActorRef) then spawn mailbox.Context id <| aggregateFunc // reconstitute aggregate state here if pre-existing
                        else child
        aggregate.Tell (actualMessage, sender)
        return! loop aggregateIds
    }
    loop Set.empty

type DomainModel(name) =
    let mutable aggregateTypeRegistry = Map.empty
    let system = System.create "system" <| Configuration.load ()
    member this.AggregateOf (typeName, id) =
        let (AggregateType cacheActor) = aggregateTypeRegistry |> Map.find typeName
        cacheActor <! RegisterAggregateId(id)
        AggregateRef(id, cacheActor)
    member this.RegisterAggregateType (typeName, aggregateFunc) =
        let actorRef = spawn system typeName <| aggregateCache aggregateFunc
        aggregateTypeRegistry <- aggregateTypeRegistry |> Map.add typeName (AggregateType actorRef)
    member this.Shutdown () = system.Shutdown ()

let orderType = "Order"
let model = DomainModel("OrderProcessing")
model.RegisterAggregateType (orderType, order)
let orderRef = model.AggregateOf (orderType, "123")

orderRef <! InitializeOrder 249.95m
orderRef <! ProcessOrder
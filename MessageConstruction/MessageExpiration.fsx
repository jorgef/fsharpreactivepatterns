#load "../References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

let currentTimeMillis () = DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond

type Money = Money of decimal
type PlaceOrder = { Id: string; OccurredOn: int64; TimeToLive: int64; ItemId: string; Price: Money } with
    static member Create (itemId, id, price, timeToLive) = { Id = id; OccurredOn = currentTimeMillis (); TimeToLive = timeToLive; ItemId = itemId; Price = price }

let inline isExpired (message: ^a) = 
    let occurredOn = (^a: (member get_OccurredOn: unit -> int64) message)
    let timeToLive = (^a: (member get_TimeToLive: unit -> int64) message)
    let elapsed = currentTimeMillis () - occurredOn
    elapsed > timeToLive

let purchaseRouter purchaseAgent (mailbox: Actor<_>) =
    let random = Random ()
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let millis = random.Next 100 + 1
        printfn "PurchaseRouter: delaying delivery of %A for %i milliseconds" message millis
        let duration = TimeSpan.FromMilliseconds <| float millis
        mailbox.Context.System.Scheduler.ScheduleTellOnce (duration, purchaseAgent, message)
        return! loop ()
    }
    loop ()

let purchaseAgent (mailbox: Actor<PlaceOrder>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        if isExpired message then
            mailbox.Context.System.DeadLetters <! message
            printfn "PurchaseAgent: delivered expired %A to dead letters" message
        else
            printfn "PurchaseAgent: placing order for %A" message
        return! loop ()
    }
    loop ()

let purchaseAgentRef = spawn system "purchaseAgent" purchaseAgent
let purchaseRouterRef = spawn system "purchaseRouter" <| purchaseRouter purchaseAgentRef

purchaseRouterRef <! PlaceOrder.Create ("1", "11", (Money 50.00m), 1000L)
purchaseRouterRef <! PlaceOrder.Create ("2", "22", (Money 250.00m), 100L)
purchaseRouterRef <! PlaceOrder.Create ("3", "33", (Money 32.95m), 10L)
#load "../References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()


// Scalar Messsages

let scalarValuePrinterActor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? string as msg -> printfn "ScalarValuePrinter: received String %s" msg
        | :? int as msg -> printfn "ScalarValuePrinter: received Int %i" msg
        | _ -> ()
        return! loop ()
    }
    loop ()

let scalarValuePrinter = spawn system "scalarValuePrinter" scalarValuePrinterActor

scalarValuePrinter <! 1
scalarValuePrinter <! "hello"


// Command and Event Messages

type Symbol = Symbol of string
type Money = Money of decimal

type OrderProcessorCommand =
    | ExecuteBuyOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money

type OrderProcessorEvent =
    | BuyOrderExecuted of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | SellOrderExecuted of portfolioId: string * symbol: Symbol * quantity: int * price: Money

let orderProcessorActor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(i, s, q, p) -> mailbox.Sender () <! BuyOrderExecuted(i, s, q, p)
        | ExecuteSellOrder(i, s, q, p) -> mailbox.Sender () <! SellOrderExecuted(i ,s, q, p)
        return! loop ()
    }
    loop ()

let callerActor orderProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | BuyOrderExecuted(i, Symbol(s), q, Money(p)) -> printfn "BuyOrderExecuted %s %s %i %M" i s q p
        | SellOrderExecuted(i, Symbol(s), q, Money(p)) -> printfn "SellOrderExecuted %s %s %i %M" i s q p
        return! loop ()
    }
    orderProcessor <! ExecuteBuyOrder("1", Symbol("S1"), 5, Money(10m))
    orderProcessor <! ExecuteSellOrder("2", Symbol("S2"), 3, Money(8m))
    loop ()

let orderProcessor = spawn system "orderProcessor" orderProcessorActor
let caller = spawn system "caller" <| callerActor orderProcessor
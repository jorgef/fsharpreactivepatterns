#load "..\References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
type ExecuteBuyOrder = { PortfolioId: string; Symbol: string; Quantity: int; Price: Money; DateTimeOrdered: DateTimeOffset option; Version: int } with
    static member CreateV1 (portfolioId, symbol, quantity, price) = { PortfolioId = portfolioId; Symbol = symbol; Quantity = quantity; Price = price; DateTimeOrdered = None; Version = 1 } 
    static member CreateV2 (portfolioId, symbol, quantity, price) = { PortfolioId = portfolioId; Symbol = symbol; Quantity = quantity; Price = price; DateTimeOrdered = Some DateTimeOffset.UtcNow; Version = 2 } 

let stockTrader (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let orderExecutionStartedOn = match message.Version with
                                      | 1 -> Some DateTimeOffset.UtcNow
                                      | _ -> message.DateTimeOrdered
        printfn "StockTrader: orderExecutionStartedOn: %A" orderExecutionStartedOn
        return! loop () 
    }
    loop ()

let stockTraderRef = spawn system "stockTrader" stockTrader
stockTraderRef <! ExecuteBuyOrder.CreateV1 ("1", "11", 10, (Money 50.00m))
stockTraderRef <! ExecuteBuyOrder.CreateV2 ("1", "11", 10, (Money 50.00m))
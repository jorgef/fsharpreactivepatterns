#load "../References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
type TradingCommand =
    | ExecuteBuyOrder of portfolioId: string * symbol: string * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: string * quantity: int * price: Money

let stockTrader (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(portfolioId, symbol, quantity, price) as buy -> 
            printfn "StockTrader: buying for: %A" buy
        | ExecuteSellOrder(portfolioId, symbol, quantity, price) as sell ->
            printfn "StockTrader: selling for: %A" sell
        return! loop () 
    }
    loop ()

let stockTraderRef = spawn system "stockTrader" <| stockTrader

stockTraderRef <! ExecuteBuyOrder("p123", "MSFT", 100, Money 31.85m)
stockTraderRef <! ExecuteSellOrder("p456", "MSFT", 200, Money 31.80m)
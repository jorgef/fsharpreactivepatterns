#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Symbol = Symbol of string
type Money = Money of decimal
type Market = Market of string
type ServiceResult = { PortfolioId: string; Symbol: Symbol; Quantity: int; OrderId: int; TotalCost: Money }
type BuyerService () =
    member this.PlaceBuyOrder (portfolioId: string, symbol: Symbol, quantity: int, price: Money) = 
        let (Money p) = price
        { PortfolioId = portfolioId; Symbol = symbol; Quantity = quantity; OrderId = 2; TotalCost = Money (p * 0.01m) }
type SellerService () =
    member this.PlaceSellOrder (portfolioId: string, symbol: Symbol, quantity: int, price: Money) =
        let (Money p) = price
        { PortfolioId = portfolioId; Symbol = symbol; Quantity = quantity; OrderId = 1; TotalCost = Money (p * 0.05m) }
type RegisterCommandHandler = RegisterCommandHandler of applicationId: string * commandName: string * handler: IActorRef
type Command =
    | ExecuteBuyOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money
type Event =
    | BuyOrderExecuted of portfolioId: string * orderId: int * symbol: Symbol * quantity: int * totalCost: Money
    | SellOrderExecuted of portfolioId: string * orderId: int * symbol: Symbol * quantity: int * totalCost: Money
type TradingNotification = TradingNotification of string * Event 

let stockTrader (tradingBus: IActorRef) (buyerService: BuyerService) (sellerService: SellerService) (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteBuyOrder", mailbox.Self)
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteSellOrder", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(i, s, q, p) -> 
            let result = buyerService.PlaceBuyOrder (i, s, q, p)
            tradingBus <! TradingNotification("BuyOrderExecuted", BuyOrderExecuted(result.PortfolioId, result.OrderId, result.Symbol, result.Quantity, result.TotalCost))
        | ExecuteSellOrder(i, s, q, p) -> 
            let result = buyerService.PlaceBuyOrder (i, s, q, p)
            tradingBus <! TradingNotification("SellOrderExecuted", SellOrderExecuted(result.PortfolioId, result.OrderId, result.Symbol, result.Quantity, result.TotalCost))
        return! loop ()
    }
    loop ()

let tradingBus (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "TradingBus: received %A" message
        return! loop ()
    }
    loop ()

let tradingBusRef = spawn system "tradingBus" tradingBus
let stockTraderRef = spawn system "stockTrader" (stockTrader <| tradingBusRef <| new BuyerService() <| new SellerService ())

stockTraderRef <! ExecuteBuyOrder("1", Symbol "S1", 5, Money 10m)
stockTraderRef <! ExecuteSellOrder("2", Symbol "S2", 3, Money 8m)
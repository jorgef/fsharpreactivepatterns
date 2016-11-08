#load "../References.fsx"

open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Money = Money of decimal
type TradingCommand =
    | ExecuteBuyOrder of portfolioId: string * symbol: string * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: string * quantity: int * price: Money
type TradingNotification =
    | BuyOrderExecuted of portfolioId: string * symbol: string * quantity: int * price: Money
    | SellOrderExecuted of portfolioId: string * symbol: string * quantity: int * price: Money
type TradingBusMessage =
    | RegisterCommandHandler of applicationId: string * commandId: string *  handler: IActorRef
    | RegisterNotificationInterest of applicationId: string * notificationId: string * interested: IActorRef
    | TradingCommand of commandId: string * command: TradingCommand
    | TradingNotification of notificationId: string * notification: TradingNotification
    | Status
type CommandHandler = CommandHandler of applicationId: string * handler: IActorRef
type NotificationInterest = NotificationInterest of applicationId: string * interested: IActorRef

let tradingBus (mailbox: Actor<_>) =
    let rec loop commandHandlers notificationInterests = actor {
        let dispatchCommand commandId command =
            commandHandlers 
            |> Map.tryFind commandId
            |> Option.map (fun hs -> hs |> List.iter (fun (CommandHandler(_, h)) -> h <! command))
            |> ignore

        let dispatchNotification notificationId notification =
            notificationInterests 
            |> Map.tryFind notificationId
            |> Option.map (fun hs -> hs |> List.iter (fun (NotificationInterest(_, i)) -> i <! notification))
            |> ignore

        let registerCommandHandler commandId applicationId handler =
            let commandHandler = CommandHandler(applicationId, handler)
            commandHandlers
            |> Map.tryFind commandId
            |> Option.fold (fun _ hs -> commandHandler :: hs) [commandHandler]
            |> fun hs -> Map.add commandId hs commandHandlers

        let registerNotificationInterest notificationId applicationId interested =
            let notificationInterest = NotificationInterest(applicationId, interested)
            notificationInterests
            |> Map.tryFind notificationId
            |> Option.fold (fun _ is -> notificationInterest :: is) [notificationInterest]
            |> fun is -> Map.add notificationId is notificationInterests

        let! message = mailbox.Receive ()
        match message with
        | RegisterCommandHandler(applicationId, commandId, handler) -> 
            return! loop (registerCommandHandler commandId applicationId handler) notificationInterests
        | RegisterNotificationInterest(applicationId, notificationId, interested) -> 
            return! loop commandHandlers (registerNotificationInterest notificationId applicationId interested)
        | TradingCommand(commandId, command) -> dispatchCommand commandId command
        | TradingNotification(notificationId, notification) -> dispatchNotification notificationId notification
        | Status -> 
            printfn "TradingBus: STATUS: %A" commandHandlers
            printfn "TradingBus: STATUS: %A" notificationInterests
        return! loop commandHandlers notificationInterests
    }
    loop Map.empty Map.empty

let marketAnalysisTools tradingBus (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterNotificationInterest(applicationId, "BuyOrderExecuted", mailbox.Self)
    tradingBus <! RegisterNotificationInterest(applicationId, "SellOrderExecuted", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | BuyOrderExecuted _ as executed -> printfn "MarketAnalysisTools: adding holding: %A" executed
        | SellOrderExecuted _ as executed -> printfn "MarketAnalysisTools: adjusting holding: %A" executed
        return! loop () 
    }
    loop ()

let portfolioManager tradingBus (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterNotificationInterest(applicationId, "BuyOrderExecuted", mailbox.Self)
    tradingBus <! RegisterNotificationInterest(applicationId, "SellOrderExecuted", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | BuyOrderExecuted _ as executed -> printfn "PortfolioManager: adding holding: %A" executed
        | SellOrderExecuted _ as executed -> printfn "PortfolioManager: adjusting holding: %A" executed
        return! loop () 
    }
    loop ()

let stockTrader tradingBus (mailbox: Actor<_>) =
    let applicationId = mailbox.Self.Path.Name
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteBuyOrder", mailbox.Self)
    tradingBus <! RegisterCommandHandler(applicationId, "ExecuteSellOrder", mailbox.Self)
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(portfolioId, symbol, quantity, price) as buy -> 
            printfn "StockTrader: buying for: %A" buy
            tradingBus <! TradingNotification("BuyOrderExecuted", BuyOrderExecuted(portfolioId, symbol, quantity, price))
        | ExecuteSellOrder(portfolioId, symbol, quantity, price) as sell ->
            printfn "StockTrader: selling for: %A" sell
            tradingBus <! TradingNotification("BuyOrderExecuted", SellOrderExecuted(portfolioId, symbol, quantity, price))
        return! loop () 
    }
    loop ()

let tradingBusRef = spawn system "tradingBus" tradingBus
let marketAnalysisToolsRef = spawn system "marketAnalysisTools" <| marketAnalysisTools tradingBusRef
let portfolioManagerRef = spawn system "portfolioManager" <| portfolioManager tradingBusRef
let stockTraderRef = spawn system "stockTrader" <| stockTrader tradingBusRef

tradingBusRef <! Status
tradingBusRef <! TradingCommand("ExecuteBuyOrder", ExecuteBuyOrder("p123", "MSFT", 100, Money 31.85m))
tradingBusRef <! TradingCommand("ExecuteSellOrder", ExecuteSellOrder("p456", "MSFT", 200, Money 31.80m))
tradingBusRef <! TradingCommand("ExecuteBuyOrder", ExecuteBuyOrder("p789", "MSFT", 100, Money 31.83m))
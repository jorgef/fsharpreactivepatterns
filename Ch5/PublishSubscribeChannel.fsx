#load "..\References.fsx"

open Akka.FSharp

let system = System.create "TradingSystem" <| Configuration.load ()

type Money = Money of amount: decimal
type Market = Market of name: string
type Symbol = Symbol of name: string
type PricedQuoted = { Market: Market; Ticker: Symbol; Price: Money }

let quoteListener (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! { Market = Market m; Ticker = Symbol t; Price = Money p } = mailbox.Receive ()
        printfn "QuoteListener: PricedQuoted received, market: %s, ticker: %s, price: %M" m t p 
        return! loop ()
    }
    loop () 

let quoteListenerRef = spawn system "quoteListenerRef" quoteListener
subscribe typeof<PricedQuoted> quoteListenerRef system.EventStream
publish { Market = Market("quotes/NASDAQ"); Ticker = Symbol "MSFT"; Price = Money(37.16m) } system.EventStream
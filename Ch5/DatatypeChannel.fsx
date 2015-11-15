#load "..\References.fsx"

open System.Text
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type ProductQuery = ProductQuery of string

let productQueriesChannel (mailbox: Actor<_>) =
    let translateToProductQuery message = message |> Encoding.UTF8.GetString |> ProductQuery
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let (ProductQuery value) = message |> translateToProductQuery
        printfn "ProductQueriesChannel: ProductQuery received, value: %s" <| value
        return! loop ()
    }
    loop ()

let productQueriesChannelRef = spawn system "productQueriesChannel" productQueriesChannel

productQueriesChannelRef <! Encoding.UTF8.GetBytes "test query"
#load "../References.fsx"

open System
open System.Text
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type ProcessIncomingOrder = ProcessIncomingOrder of byte array

let orderAcceptanceEndpoint nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        let text = Encoding.Default.GetString message
        printfn "OrderAcceptanceEndpoint: processing %s" text
        nextFilter <! ProcessIncomingOrder(message)
        return! loop ()
    }
    loop ()

let decrypter nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "Decrypter: processing %s" text
        let orderText = text.Replace ("(encryption)", String.Empty)
        nextFilter <! ProcessIncomingOrder(Encoding.Default.GetBytes orderText)
        return! loop ()
    }
    loop ()

let authenticator nextFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "Authenticator: processing %s" text
        let orderText = text.Replace ("(certificate)", String.Empty)
        nextFilter <! ProcessIncomingOrder(Encoding.Default.GetBytes orderText)
        return! loop ()
    }
    loop ()

let deduplicator nextFilter (mailbox: Actor<_>) =
    let orderIdFrom (orderText: string) =
        let orderIdIndex = orderText.IndexOf ("id='") + 4
        let orderIdLastIndex = orderText.IndexOf ("'", orderIdIndex)
        orderText.Substring (orderIdIndex, orderIdLastIndex)

    let rec loop (processedOrderIds: string Set) = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "Deduplicator: processing %s" text
        let orderId = orderIdFrom text
        if (not <| Set.contains orderId processedOrderIds) then 
            nextFilter <! ProcessIncomingOrder(bytes) 
            return! loop <| Set.add orderId processedOrderIds
        else 
            printfn "Deduplicator: found duplicate order %s" orderId
            return! loop processedOrderIds
    }
    loop Set.empty

let orderManagerSystem (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! ProcessIncomingOrder(bytes) = mailbox.Receive ()
        let text = Encoding.Default.GetString bytes
        printfn "OrderManagementSystem: processing unique order: %s" text
        return! loop ()
    }
    loop ()

let orderText = "(encryption)(certificate)<order id='123'>...</order>"
let rawOrderBytes = Encoding.Default.GetBytes orderText

let filter5 = spawn system "orderManagementSystem" orderManagerSystem
let filter4 = spawn system "deduplicator" <| deduplicator filter5
let filter3 = spawn system "authenticator" <| authenticator filter4
let filter2 = spawn system "decrypter" <| decrypter filter3
let filter1 = spawn system "orderAcceptanceEndpoint" <| orderAcceptanceEndpoint filter2

filter1 <! rawOrderBytes
filter1 <! rawOrderBytes

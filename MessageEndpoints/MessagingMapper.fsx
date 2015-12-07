#load "..\References.fsx"

open System
open Akka.Actor
open Akka.FSharp
open Newtonsoft.Json

let system = System.create "system" <| Configuration.load ()

type QueryMonthlyOrdersFor = QueryMonthlyOrdersFor of customerId: string
type ReallyBigQueryResult = ReallyBigQueryResult of messageBody: string

type IMessageSerializer =
    abstract member Serialize: obj -> string
    abstract member Deserialize: string -> 'a

let serializer = { 
    new IMessageSerializer with
        member this.Serialize obj = JsonConvert.SerializeObject obj
        member this.Deserialize json = JsonConvert.DeserializeObject<'a>(json) 
    }
        
let orderQueryService (serializer: IMessageSerializer) (mailbox: Actor<_>) =
    let monthlyOrdersFor customerId = [ for i in [1 .. 10] -> sprintf "Order data %i" i ]
    let rec loop () = actor {
        let! QueryMonthlyOrdersFor(customerId) as message = mailbox.Receive ()
        printfn "OrderQueryService: Received %A" message
        let queryResult = monthlyOrdersFor customerId
        let messageBody = serializer.Serialize(queryResult)
        mailbox.Sender () <! ReallyBigQueryResult messageBody
        return! loop ()
    }
    loop ()

let caller orderQueryService (mailbox: Actor<_>) =
    orderQueryService <! QueryMonthlyOrdersFor "123"
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        printfn "Caller: result received: %A" message
        return! loop ()
    }
    loop ()

let orderQueryServiceRef = spawn system "orderQueryService" <| orderQueryService serializer
let callerRef = spawn system "caller" <| caller orderQueryServiceRef
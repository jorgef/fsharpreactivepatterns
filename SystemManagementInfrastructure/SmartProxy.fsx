#load "..\References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type ServiceRequest =
    | ServiceRequestOne of requestId : string
    | ServiceRequestTwo of requestId : string
    | ServiceRequestThree of requestId : string with
    member this.RequestId with get () = match this with | ServiceRequestOne requestId | ServiceRequestTwo requestId | ServiceRequestThree requestId -> requestId
type ServiceReply = 
    | ServiceReplyOne of replyId : string
    | ServiceReplyTwo of replyId : string
    | ServiceReplyThree of replyId : string with 
    member this.ReplyId with get () = match this with | ServiceReplyOne replyId | ServiceReplyTwo replyId | ServiceReplyThree replyId -> replyId 
type RequestService = RequestService of service: ServiceRequest

let serviceProvider (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ServiceRequestOne requestId -> mailbox.Sender () <! ServiceReplyOne requestId
        | ServiceRequestTwo requestId -> mailbox.Sender () <! ServiceReplyTwo requestId
        | ServiceRequestThree requestId -> mailbox.Sender () <! ServiceReplyThree requestId
        return! loop ()
    }
    loop ()

let serviceRequester serviceProvider (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? RequestService as request -> 
            printfn "ServiceRequester: %s: %A" mailbox.Self.Path.Name request
            let (RequestService service) = request
            serviceProvider <! service
        | reply -> printfn "ServiceRequester: %s: %A" mailbox.Self.Path.Name reply
        return! loop ()
    }
    loop ()

let serviceProviderProxy serviceProvider (mailbox: Actor<_>) =
    let analyzeReply reply = printfn "Reply analyzed: %A" reply
    let analyzeRequest request = printfn "Request analyzed: %A" request

    let rec loop requesters = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? ServiceRequest as request ->
            let requesters = requesters |> Map.add request.RequestId (mailbox.Sender ())
            serviceProvider <! request
            analyzeRequest request
            return! loop requesters
        | :? ServiceReply as reply -> 
            let requester = requesters |> Map.tryFind reply.ReplyId
            match requester with 
            | Some sender ->
                analyzeReply reply
                sender <! reply
                let requesters = requesters |> Map.remove reply.ReplyId
                return! loop requesters
            | None -> return! loop requesters
        | _ -> return! loop requesters
    }
    loop Map.empty

let serviceProviderRef = spawn system "serviceProvider" serviceProvider
let proxyRef = spawn system "proxy" <| serviceProviderProxy serviceProviderRef
let requester1Ref = spawn system "requester1" <| serviceRequester proxyRef
let requester2Ref = spawn system "requester2" <| serviceRequester proxyRef
let requester3Ref = spawn system "requester3" <| serviceRequester proxyRef

requester1Ref <! RequestService(ServiceRequestOne "1")
requester2Ref <! RequestService(ServiceRequestTwo "2")
requester3Ref <! RequestService(ServiceRequestThree "3")
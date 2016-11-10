#load "../References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type Who = Who of name: string
type What = What of happened: string
type Where = Where of actorType: string * actorName: string
type Why = Why of explanation: string
type Entry = { Who: Who; What: What; Where: Where; When: DateTimeOffset; Why: Why } with
    static member Create (who, what, where, ``when``, why) = { Who = who; What = what; Where = where; When = ``when``; Why = why }
    static member Create (who: Who, what: What, where: Where, why: Why) = Entry.Create (who, what, where, DateTimeOffset.UtcNow, why)
    static member Create (who, what, actorType, actorName, why) =  Entry.Create (Who who, What what, Where(actorType, actorName), Why why)
    member this.AsMetadata () = (Metadata.Create () : Metadata).Including this
and Metadata = { Entries: Entry list } with
    static member Create () = { Entries = [] }
    member this.Including entry = { Entries = this.Entries @ [entry] }
type SomeMessage = SomeMessage of payload: string * metadata: Metadata with
    static member Create payload = SomeMessage(payload, Metadata.Create ())
    member this.Including entry =
        let (SomeMessage(payload, metadata)) = this 
        SomeMessage(payload, metadata.Including entry)

let processor next (mailbox: Actor<SomeMessage>) =
    let random = Random()
    let user = sprintf "user%i" (random.Next 100)
    let wasProcessed = sprintf "Processed: %i" (random.Next 5)
    let because = sprintf "Because: %i" (random.Next 10)
    let entry = Entry.Create (Who user, What wasProcessed, Where("processor", mailbox.Self.Path.Name), DateTimeOffset.UtcNow, Why because)
    let report message heading = printfn "%s %s: %A" mailbox.Self.Path.Name (defaultArg heading "received") message
    
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        report message None
        let nextMessage = message.Including entry
        match next with
        | Some next -> next <! nextMessage
        | None -> report nextMessage <| Some "complete"
        return! loop ()
    }
    loop ()

let processor3Ref = spawn system "processor3" <| processor None
let processor2Ref = spawn system "processor2" <| processor (Some processor3Ref)
let processor1Ref = spawn system "processor1" <| processor (Some processor2Ref)

let entry = Entry.Create (Who "driver", What "Started", Where("processor", "driver"), DateTimeOffset.UtcNow, Why "Running processors")
processor1Ref <! SomeMessage("Data...", entry.AsMetadata ())
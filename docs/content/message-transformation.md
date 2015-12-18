#Message Transformation

For more details and full analysis of the patterns described in this section, please refer to **Chapter 8** of <a href="http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">Reactive Messaging Patterns with the Actor Model</a> by <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a>.


##Sections

1. [Introduction](index.html)
2. [Messaging with Actors](messaging-with-actors.html)
3. [Messaging Channels](messaging-channels.html)
4. [Message Construction](message-construction.html)
5. [Message Routing](message-routing.html)
6. **Message Transformation**
	- [Envelope Wrapper](#Envelope-Wrapper)
	- [Content Enricher](#Content-Enricher)
	- [Content Filter](#Content-Filter)
	- [Claim Check](#Claim-Check)
	- [Normalizer](#Normalizer)
	- [Canonical Message Model](#Canonical-Message-Model)
7. [Message Endpoints](message-endpoints.html)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Envelope Wrapper

This pattern wraps messages received from external sources.

```fsharp
type IReplyToSupport =
    abstract member Reply: obj -> unit
    abstract member SetUpReplyToSupport: string -> unit
type IRegisterCustomer = 
    inherit IReplyToSupport
    abstract member Message: string with get
type RabbitMQReplyToSupport() =
    let mutable returnAddress = String.Empty
    interface IReplyToSupport with
        member this.Reply message = printfn "RabbitMQReplyToSupport: Replying %A to \"%s\"" message returnAddress
        member this.SetUpReplyToSupport replyReturnAddress = returnAddress <- replyReturnAddress
type RegisterCustomerRabbitMQReplyToMapEnvelope(mapMessage: Map<string, string>) as this =
    inherit RabbitMQReplyToSupport()
    let this = this :> IReplyToSupport
    do this.SetUpReplyToSupport(mapMessage |> Map.find "returnAddress")
    let message = mapMessage |> Map.find "message"
    interface IRegisterCustomer with
        member this.Message with get () = message

let customerRegistrar (mailbox: Actor<IRegisterCustomer>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        printfn "CustomerRegistrar: Received \"%s\"" registerCustomer.Message
        registerCustomer.Reply "hi"
        return! loop ()
    }
    loop ()

let receivedMessageAsMap _ = [ ("returnAddress", "http://caller/"); ("message", "hello") ] |> Map.ofList
let wireMessage = ()
let mapMessage = receivedMessageAsMap wireMessage

let customerRegistrarRef = spawn system "customerRegistrar" customerRegistrar

let registerCustomer = RegisterCustomerRabbitMQReplyToMapEnvelope(mapMessage)
customerRegistrarRef <! registerCustomer
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageTransformation/EnvelopeWrapper.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Content Enricher

This pattern adapts messages by adding or removing information so they are suitable for other systems.

```fsharp
type PatientDetails = { LastName: string; SocialSecurityNumber: string; Carrier: string }
type DoctorVisitCompleted = 
    DoctorVisitCompleted of patientId: string * firstName: string * date: DateTimeOffset * carrier: string option * lastName: string option * socialSecurityNumber: string option with
    static member Create (patientId, firstName, date, patientDetails) = 
        DoctorVisitCompleted(patientId, firstName, date, Some patientDetails.Carrier, Some patientDetails.LastName, Some patientDetails.SocialSecurityNumber)
    static member Create (patientId, firstName, date) = DoctorVisitCompleted(patientId, firstName, date, None, None, None)
type VisitCompleted = VisitCompleted of dispatcher: IActorRef

let accountingEnricherDispatcher (accountingSystemDispatcher: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! (DoctorVisitCompleted(patientId, firstName, date, _, _, _)) = mailbox.Receive ()
        printfn "AccountingEnricherDispatcher: querying and forwarding."
        let lastName = "Doe"
        let carrier = "Kaiser"
        let socialSecurityNumber = "111-22-3333"
        let patientDetails = { LastName = lastName; SocialSecurityNumber = socialSecurityNumber; Carrier = carrier }
        let enrichedDoctorVisitCompleted = DoctorVisitCompleted.Create (patientId, firstName, date, patientDetails)
        accountingSystemDispatcher.Forward enrichedDoctorVisitCompleted
        return! loop ()
    }
    loop ()

let accountingSystemDispatcher (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! doctorVisitCompleted = mailbox.Receive ()
        printfn "AccountingSystemDispatcher: sending to Accounting System..."
        return! loop ()
    }
    loop ()

let scheduledDoctorVisit patientId firstName (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! (VisitCompleted dispatcher) = mailbox.Receive ()
        printfn "ScheduledDoctorVisit: completing visit."
        let completedOn = DateTimeOffset.UtcNow
        dispatcher <! DoctorVisitCompleted.Create(patientId, firstName, completedOn)
        return! loop ()
    }
    loop ()

let accountingSystemDispatcherRef = spawn system "accountingSystem" accountingSystemDispatcher
let accountingEnricherDispatcherRef = spawn system "accountingDispatcher" <| accountingEnricherDispatcher accountingSystemDispatcherRef
let scheduledDoctorVisitRef = spawn system "scheduledVisit" <| scheduledDoctorVisit "123456789" "John"

scheduledDoctorVisitRef <! VisitCompleted(accountingEnricherDispatcherRef)
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageTransformation/ContentEnricher.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Content Filter

The Content Filter pattern reduces or simplifies messages by removing information not required by the target.

```fsharp
type Message =
    | FilteredMessage of light: string * ``and``: string * fluffy: string * message: string
    | UnfilteredPayload of largePayload: string

let messageContentFilter (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | UnfilteredPayload payload -> 
            printfn "MessageContentFilter: received unfiltered message: %s" payload
            mailbox.Sender () <! FilteredMessage("this", "feels", "so", "right")
        | FilteredMessage _ -> printfn "MessageContentFilter: unexpected"
        return! loop ()
    }
    loop ()

let messageExchangeDispatcher (mailbox: Actor<_>) =
    let messageContentFilter = spawn mailbox.Context "messageContentFilter" messageContentFilter
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | UnfilteredPayload payload ->
            printfn "MessageExchangeDispatcher: received unfiltered message: %s" payload
            messageContentFilter <! message
        | FilteredMessage _ -> printfn "MessageExchangeDispatcher: dispatching: %A" message
        return! loop ()
    }
    loop ()

let messageExchangeDispatcherRef = spawn system "messageExchangeDispatcher" messageExchangeDispatcher

messageExchangeDispatcherRef <! UnfilteredPayload "A very large message with complex structure..."
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageTransformation/ContentFilter.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Claim Check

The Claim Check pattern splits a message into smaller parts and allowing access to them on demand. 

```fsharp
type Part = Part of name: string
type ClaimCheck = ClaimCheck of number: string with
    static member Create () = ClaimCheck((Guid.NewGuid ()).ToString ())
type CheckedItem = CheckedItem of claimCheck: ClaimCheck * businessId: string * parts: Map<string, Part>
type CheckedPart = CheckedPart of claimCheck: ClaimCheck * partName: string * part: obj
type ProcessStep = ProcessStep of id: string * claimCheck: ClaimCheck
type ProcessMessage =
    | CompositeMessage of id: string * part1: Part * part2: Part * part3: Part
    | StepCompleted of id: string * claimCheck: ClaimCheck * stepName: string
type ItemChecker() =
    let mutable checkedItems = Map.empty
    member this.CheckedItemFor (businessId, parts) = CheckedItem(ClaimCheck.Create(), businessId, parts)
    member this.CheckItem (CheckedItem(claimCheck, businessId, parts) as item) = checkedItems <- checkedItems |> Map.add claimCheck item
    member this.ClaimItem claimCheck = checkedItems |> Map.find claimCheck
    member this.ClaimPart (claimCheck, partName) = 
        let (CheckedItem(_, _, parts)) = checkedItems |> Map.find claimCheck
        CheckedPart(claimCheck, partName, parts |> Map.find partName)
    member this.RemoveItem claimCheck = checkedItems <- checkedItems |> Map.remove claimCheck

let ``process`` steps (itemChecker: ItemChecker) (mailbox: Actor<_>) =
    let rec loop stepIndex = actor {
        let! message = mailbox.Receive ()
        match message with
        | CompositeMessage(id, (Part(part1Name) as part1), (Part(part2Name) as part2), (Part(part3Name) as part3)) -> 
            let parts = [ (part1Name, part1); (part2Name, part2); (part3Name, part3) ] |> Map.ofList
            let (CheckedItem(claimCheck, _, _) as checkedItem) = itemChecker.CheckedItemFor (id, parts)
            itemChecker.CheckItem checkedItem
            steps |> List.item stepIndex <! ProcessStep(id, claimCheck)
            return! loop stepIndex
        | StepCompleted(id, claimCheck, stepName) -> 
            if stepIndex < steps.Length then steps |> List.item stepIndex <! ProcessStep(id, claimCheck)
            else itemChecker.RemoveItem claimCheck
            return! loop <| stepIndex + 1
    }
    loop 0

let step1 (itemChecker: ItemChecker) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! (ProcessStep(id, claimCheck) as processStep) = mailbox.Receive ()
        let claimedPart = itemChecker.ClaimPart (claimCheck, "partA1")
        printfn "Step1: processing %A with %A" processStep claimedPart
        mailbox.Sender () <! StepCompleted(id, claimCheck, "step1")
        return! loop ()
    }
    loop ()

let step2 (itemChecker: ItemChecker) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! (ProcessStep(id, claimCheck) as processStep) = mailbox.Receive ()
        let claimedPart = itemChecker.ClaimPart (claimCheck, "partB2")
        printfn "Step2: processing %A with %A" processStep claimedPart
        mailbox.Sender () <! StepCompleted(id, claimCheck, "step2")
        return! loop ()
    }
    loop ()

let step3 (itemChecker: ItemChecker) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! (ProcessStep(id, claimCheck) as processStep) = mailbox.Receive ()
        let claimedPart = itemChecker.ClaimPart (claimCheck, "partC3")
        printfn "Step3: processing %A with %A" processStep claimedPart
        mailbox.Sender () <! StepCompleted(id, claimCheck, "step3")
        return! loop ()
    }
    loop ()

let itemChecker = ItemChecker()
let step1Ref = spawn system "step1" <| step1 itemChecker
let step2Ref = spawn system "step2" <| step2 itemChecker
let step3Ref = spawn system "step3" <| step3 itemChecker
let processRef = spawn system "process" <| ``process`` [step1Ref; step2Ref; step3Ref] itemChecker

processRef <! CompositeMessage("ABC", Part("partA1"), Part("partB2"), Part("partC3"))
```

<a href="https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessageTransformation/ClaimCheck.fsx" target="_blank">Complete Code</a>

[Sections](#Sections)

##Normalizer

The normalizer pattern transforms unsupported messages to supported ones.

```fsharp
// No code example
```

[Sections](#Sections)

##Canonical Message Model

This pattern defines a common set of messages shared by multiple applications.

```fsharp
// No code example
```

[Sections](#Sections)
#load "..\References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type PatientDetails = { LastName: string; SocialSecurityNumber: string; Carrier: string }
type DoctorVisitCompleted = DoctorVisitCompleted of patientId: string * firstName: string * date: DateTimeOffset * carrier: string option * lastName: string option * socialSecurityNumber: string option with
    static member Create (patientId, firstName, date, patientDetails) = DoctorVisitCompleted(patientId, firstName, date, Some patientDetails.Carrier, Some patientDetails.LastName, Some patientDetails.SocialSecurityNumber)
    static member Create (patientId, firstName, date) = DoctorVisitCompleted(patientId, firstName, date, None, None, None)
type VisitCompleted = VisitCompleted of dispatcher: IActorRef

let accountingEnricherDispatcher (accountingSystemDispatcher: IActorRef) (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! (DoctorVisitCompleted(patientId, firstName, date, _, _, _)) = mailbox.Receive ()
        printfn "AccountingEnricherDispatcher: querying and forwarding."
        let lastName = "Doe"
        let carrier = "Kaiser"
        let socialSecurityNumber = "111-22-3333"
        let enrichedDoctorVisitCompleted = DoctorVisitCompleted.Create (patientId, firstName, date, { LastName = lastName; SocialSecurityNumber = socialSecurityNumber; Carrier = carrier })
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
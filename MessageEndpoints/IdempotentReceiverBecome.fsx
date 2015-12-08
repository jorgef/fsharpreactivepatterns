#load "..\References.fsx"

open System
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type RiskAssessmentMessage = 
    | AttachDocument of documentText: string
    | ClassifyRisk
type RiskClassified = RiskClassified of classification: string
type Document = { Text: string option } with
    member this.DetermineClassification () =
        this.Text
        |> Option.fold (fun _ text ->
            match text.ToLower () with
            | text when text.Contains "low" -> "Low"
            | text when text.Contains "medium" -> "Medium"
            | text when text.Contains "high" -> "High"
            | _  -> "Unknown")
            "Unknown"
    member this.IsNotAttached with get () = this.Text |> Option.fold (fun _ text -> String.IsNullOrEmpty text) false 
    member this.IsDefined with get () = this.Text.IsSome

let riskAssessment (mailbox: Actor<_>) =
    let rec documented (document: Document) = actor {
        let! message = mailbox.Receive ()
        match message with
        | AttachDocument _ ->
            // already received; ignore
            return! documented document
        | ClassifyRisk ->
            mailbox.Sender () <! RiskClassified(document.DetermineClassification ())
            return! documented document
    }
    let rec undocumented (document: Document) = actor {
        let! message = mailbox.Receive ()
        match message with
        | AttachDocument documentText ->
            let document = { Text = Some documentText }
            return! documented document
        | ClassifyRisk ->
            mailbox.Sender () <! RiskClassified("Unknown")
            return! undocumented document
    }
    undocumented { Text = None }

let riskAssessmentRef = spawn system "riskAssessment" riskAssessment

let futureAssessment1: RiskClassified = riskAssessmentRef <? ClassifyRisk |> Async.RunSynchronously
printfn "%A" futureAssessment1
riskAssessmentRef <! AttachDocument("This is a HIGH risk.")
let futureAssessment2: RiskClassified = riskAssessmentRef <? ClassifyRisk |> Async.RunSynchronously
printfn "%A" futureAssessment2
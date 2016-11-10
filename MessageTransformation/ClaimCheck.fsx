#load "../References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

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
#load "../References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type PostalAddress = { Address1: string; Address2: string; City: string; State: string; ZipCode: string }
type Telephone = Telephone of number: string
type CustomerInformation = { Name: string; FederalTaxId: string }
type ContactInformation = { PostalAddress: PostalAddress; Telephone: Telephone }
type ServiceOption = { Id: string; Description: string }
type RegistrationData = { CustomerInformation: CustomerInformation; ContactInformation: ContactInformation; ServiceOption: ServiceOption }
type ProcessStep = { Name: string; Processor: IActorRef }
type RegistrationProcess = { ProcessId: string; ProcessSteps: ProcessStep list; CurrentStep: int } with
    static member Create (processId, processSteps) = { ProcessId = processId; ProcessSteps = processSteps; CurrentStep = 0 }
    member this.IsCompleted with get () = this.CurrentStep >= this.ProcessSteps.Length
    member this.NextStep () = 
        if (this.IsCompleted) then failwith "Process had already completed."
        else this.ProcessSteps |> List.item this.CurrentStep
    member this.StepCompleted () = { this with CurrentStep = this.CurrentStep + 1 }
type RegisterCustomer = { RegistrationData: RegistrationData; RegistrationProcess: RegistrationProcess } with
    member this.Advance () = 
        let advancedProcess = this.RegistrationProcess.StepCompleted ()
        if not advancedProcess.IsCompleted then (advancedProcess.NextStep ()).Processor <! { this with RegistrationProcess = advancedProcess }
        else ()

let creditChecker (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let federalTaxId = registerCustomer.RegistrationData.CustomerInformation.FederalTaxId
        printfn "CreditChecker: handling register customer to perform credit check: %s" federalTaxId
        registerCustomer.Advance ()
    }
    loop ()

let contactKeeper (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let contactInfo = registerCustomer.RegistrationData.ContactInformation
        printfn "ContactKeeper: handling register customer to keep contact information: %A" contactInfo
        registerCustomer.Advance ()
    }
    loop ()

let customerVault (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let customerInformation = registerCustomer.RegistrationData.CustomerInformation
        printfn "CustomerVault: handling register customer to create a new custoner: %A" customerInformation
        registerCustomer.Advance ()
    }
    loop ()

let servicePlanner (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! registerCustomer = mailbox.Receive ()
        let serviceOption = registerCustomer.RegistrationData.ServiceOption
        printfn "ServicePlanner: handling register customer to plan a new customer service: %A" serviceOption
        registerCustomer.Advance ()
    }
    loop ()

module ServiceRegistry =
    let contactKeeper (system: ActorSystem) (id: string) = spawn system (sprintf "contactKeeper-%s" id) contactKeeper
    let creditChecker (system: ActorSystem) (id: string) = spawn system (sprintf "creditChecker-%s" id) creditChecker
    let customerVault (system: ActorSystem) (id: string) = spawn system (sprintf "customerVault-%s" id) customerVault
    let servicePlanner (system: ActorSystem) (id: string) = spawn system (sprintf "servicePlanner-%s" id) servicePlanner

let processId = (Guid.NewGuid ()).ToString ()
let step1 = { Name = "create_customer"; Processor = ServiceRegistry.customerVault system processId }
let step2 = { Name = "set_up_contact_info"; Processor = ServiceRegistry.contactKeeper system processId }
let step3 = { Name = "select_service_plan"; Processor = ServiceRegistry.servicePlanner system processId }
let step4 = { Name = "check_credit"; Processor = ServiceRegistry.creditChecker system processId }
let registrationProcess = RegistrationProcess.Create (processId, [ step1; step2; step3; step4 ])
let registrationData = { 
    CustomerInformation = { Name = "ABC, Inc."; FederalTaxId = "123-45-6789" }
    ContactInformation = { PostalAddress = { Address1 = "123 Main Street"; Address2 = "Suite 100"; City = "Boulder"; State = "CO"; ZipCode = "80301" }
                           Telephone = Telephone "303-555-1212" }
    ServiceOption = { Id = "99-1203"; Description = "A description of 99-1203." } }
let registerCustomer = { RegistrationData = registrationData; RegistrationProcess = registrationProcess }

(registrationProcess.NextStep ()).Processor <! registerCustomer
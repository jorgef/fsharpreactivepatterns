#load "../References.fsx"

open System
open Akka.Actor
open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type BankLoanRateQuote = { BankId: string; BankLoanRateQuoteId: string; InterestRate: decimal }
type LoanBrokerMessage = 
    | ProcessStarted of processId: string * ``process``: IActorRef
    | ProcessStopped of processId: string * ``process``: IActorRef
    | QuoteBestLoanRate of taxId: string * amount: int * termInMonths: int
    | BankLoanRateQuoted of bankId: string * bankLoanRateQuoteId: string * loadQuoteReferenceId: string * taxId: string * interestRate: decimal
    | CreditChecked of creditProcessingReferenceId: string * taxId: string * score: int
    | CreditScoreForLoanRateQuoteDenied of loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * score: int
    | CreditScoreForLoanRateQuoteEstablished of loanRateQuoteId: string * taxId: string * score: int * amount: int * termInMonths: int
    | LoanRateBestQuoteFilled of loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * creditScore: int * bestBankLoanRateQuote: BankLoanRateQuote
    | LoanRateQuoteRecorded of loanRateQuoteId: string * taxId: string * bankLoanRateQuote: BankLoanRateQuote
    | LoanRateQuoteStarted of loanRateQuoteId: string * taxId: string
    | LoanRateQuoteTerminated of loanRateQuoteId: string * taxId: string
type LoanRateQuoteMessage = 
    | StartLoanRateQuote of expectedLoanRateQuotes: int
    | EstablishCreditScoreForLoanRateQuote of loanRateQuoteId: string * taxId: string * score: int
    | RecordLoanRateQuote of bankId: string * bankLoanRateQuoteId: string * interestRate: decimal
    | TerminateLoanRateQuote
type QuoteLoanRate = QuoteLoanRate of loadQuoteReferenceId: string * taxId: string * creditScore: int * amount: int * termInMonths: int
type BestLoanRateDenied = BestLoanRateDenied of loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * creditScore: int
type BestLoanRateQuoted = BestLoanRateQuoted of bankId: string * loanRateQuoteId: string * taxId: string * amount: int * termInMonths: int * creditScore: int * interestRate: decimal
type CheckCredit = CheckCredit of creditProcessingReferenceId: string * taxId: string

module Process =
    let processOf processId processes = processes |> Map.find processId
    let startProcess processId ``process`` self processes =
        self <! ProcessStarted(processId, ``process``)
        processes |> Map.add processId ``process``
    let stopProcess processId self processes =
        let ``process`` = processOf processId processes
        self <! ProcessStopped(processId, ``process``)
        processes |> Map.remove processId

let loanRateQuote loanRateQuoteId taxId amount termInMonths loanBroker (mailbox: Actor<_>) =
    let rec loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore = actor {
        let quotableCreditScore score = score > 399

        let bestBankLoanRateQuote () = bankLoanRateQuotes |> List.minBy (fun bankLoanRateQuote -> bankLoanRateQuote.InterestRate)

        let! message = mailbox.Receive () 
        match message with
        | StartLoanRateQuote expectedLoanRateQuotes -> 
            loanBroker <! LoanRateQuoteStarted(loanRateQuoteId, taxId)
            return! loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore
        | EstablishCreditScoreForLoanRateQuote(loanRateQuoteId, taxId, creditRatingScore) -> 
            if quotableCreditScore creditRatingScore then loanBroker <! CreditScoreForLoanRateQuoteEstablished(loanRateQuoteId, taxId, creditRatingScore, amount, termInMonths)
            else loanBroker <! CreditScoreForLoanRateQuoteDenied(loanRateQuoteId, taxId, amount, termInMonths, creditRatingScore)
            return! loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore
        | RecordLoanRateQuote(bankId, bankLoanRateQuoteId, interestRate) -> 
            let bankLoanRateQuote = { BankId = bankId; BankLoanRateQuoteId = bankLoanRateQuoteId; InterestRate = interestRate }
            loanBroker <! LoanRateQuoteRecorded(loanRateQuoteId, taxId, bankLoanRateQuote)
            if bankLoanRateQuotes |> List.length >= expectedLoanRateQuotes then loanBroker <! LoanRateBestQuoteFilled(loanRateQuoteId, taxId, amount, termInMonths, creditRatingScore, bestBankLoanRateQuote ())
            else ()
            return! loop <| bankLoanRateQuotes @ [bankLoanRateQuote] <| expectedLoanRateQuotes <| creditRatingScore
        | TerminateLoanRateQuote -> 
            loanBroker <! LoanRateQuoteTerminated(loanRateQuoteId, taxId)
            return! loop bankLoanRateQuotes expectedLoanRateQuotes creditRatingScore
    }
    loop [] 0 0

let loanBroker creditBureau banks (mailbox: Actor<_>) =
    let rec loop processes = actor {
        let! message = mailbox.Receive ()
        match message with
        | BankLoanRateQuoted(bankId, bankLoanRateQuoteId, loadQuoteReferenceId, taxId, interestRate) ->
            printfn "%A" message
            Process.processOf loadQuoteReferenceId processes <! RecordLoanRateQuote(bankId, bankLoanRateQuoteId, interestRate)
            return! loop processes
        | CreditChecked(creditProcessingReferenceId, taxId, score) ->
            printfn "%A" message
            Process.processOf creditProcessingReferenceId processes <! EstablishCreditScoreForLoanRateQuote(creditProcessingReferenceId, taxId, score)
            return! loop processes
        | CreditScoreForLoanRateQuoteDenied(loanRateQuoteId, taxId, amount, termInMonths, score) ->
            printfn "%A" message
            Process.processOf loanRateQuoteId processes <! TerminateLoanRateQuote
            let denied = BestLoanRateDenied(loanRateQuoteId, taxId, amount, termInMonths, score)
            printfn "Would be sent to original requester: %A" denied
            return! loop processes
        | CreditScoreForLoanRateQuoteEstablished(loanRateQuoteId, taxId, score, amount, termInMonths) ->
            printfn "%A" message
            banks |> List.iter (fun bank -> bank <! QuoteLoanRate(loanRateQuoteId, taxId, score, amount, termInMonths))
            return! loop processes
        | LoanRateBestQuoteFilled(loanRateQuoteId, taxId, amount, termInMonths, creditScore, bestBankLoanRateQuote) ->
            printfn "%A" message
            let best = BestLoanRateQuoted(bestBankLoanRateQuote.BankId, loanRateQuoteId, taxId, amount, termInMonths, creditScore, bestBankLoanRateQuote.InterestRate)
            printfn "Would be sent to original requester: %A" best
            return! loop <| Process.stopProcess loanRateQuoteId mailbox.Self processes 
        | LoanRateQuoteRecorded(loanRateQuoteId, taxId, bankLoanRateQuote) ->
            printfn "%A" message
            return! loop processes
        | LoanRateQuoteStarted(loanRateQuoteId, taxId) ->
            printfn "%A" message
            creditBureau <! CheckCredit(loanRateQuoteId, taxId)
            return! loop processes
        | LoanRateQuoteTerminated(loanRateQuoteId, taxId) ->
            printfn "%A" message
            return! loop <| Process.stopProcess loanRateQuoteId mailbox.Self processes 
        | ProcessStarted(processId, ``process``) ->
            printfn "%A" message
            ``process`` <! StartLoanRateQuote(banks.Length)
            return! loop processes
        | ProcessStopped (_,_) -> printfn "%A" message
        | QuoteBestLoanRate(taxId, amount, termInMonths) -> 
            let loanRateQuoteId = (Guid.NewGuid ()).ToString () // LoanRateQuote.id
            let loanRateQuote = spawn mailbox.Context "loanRateQuote" <| loanRateQuote loanRateQuoteId taxId amount termInMonths mailbox.Self
            return! loop <| Process.startProcess loanRateQuoteId loanRateQuote mailbox.Self processes
    }
    loop Map.empty

let creditBureau (mailbox: Actor<_>) =
    let creditRanges = [300; 400; 500; 600; 700]
    let randomCreditRangeGenerator = Random()
    let randomCreditScoreGenerator = Random()
    
    let rec loop () = actor {
        let! (CheckCredit(creditProcessingReferenceId, taxId)) = mailbox.Receive ()
        let range = creditRanges |> List.item (randomCreditRangeGenerator.Next 5)
        let score = range + randomCreditScoreGenerator.Next 20
        mailbox.Sender () <! CreditChecked(creditProcessingReferenceId, taxId, score)        
        return! loop ()
    }
    loop ()

let bank bankId primeRate ratePremium (mailbox: Actor<_>) =
    let randomDiscount = Random()
    let randomQuoteId = Random()
    let calculateInterestRate amount months creditScore = 
        let creditScoreDiscount = creditScore / 100.0m / 10.0m - ((randomDiscount.Next 5 |> decimal) * 0.05m)
        primeRate + ratePremium + ((months / 12.0m) / 10.0m) - creditScoreDiscount
    
    let rec loop () = actor {
        let! (QuoteLoanRate(loadQuoteReferenceId, taxId, creditScore, amount, termInMonths)) = mailbox.Receive ()
        let interestRate = calculateInterestRate amount (decimal termInMonths) (decimal creditScore)
        mailbox.Sender () <! BankLoanRateQuoted(bankId, (randomQuoteId.Next 1000).ToString (), loadQuoteReferenceId, taxId, interestRate)
        return! loop ()
    }
    loop ()

let creditBureauRef = spawn system "creditBureau" creditBureau
let bank1Ref = spawn system "bank1" <| bank "bank1" 2.75m 0.30m
let bank2Ref = spawn system "bank2" <| bank "bank2" 2.73m 0.31m
let bank3Ref = spawn system "bank3" <| bank "bank3" 2.80m 0.29m
let loanBrokerRef = spawn system "loanBroker" <| loanBroker creditBureauRef [bank1Ref; bank2Ref; bank3Ref]

loanBrokerRef <! QuoteBestLoanRate("111-11-1111", 100000, 84)
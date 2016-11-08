#load "../References.fsx"

open Akka.FSharp

let system = System.create "system" <| Configuration.load ()

type AccountId = AccountId of string
type TransactionId = TransactionId of string
type Money = Money of decimal
type Transaction = Transaction of transactionId: TransactionId * amount: Money
type AccountBalance = AccountBalance of accountId: AccountId * amount: Money
type AccountMessage = 
    | Deposit of transactionId: TransactionId * amount: Money
    | Withdraw of transactionId: TransactionId * amount: Money
    | QueryBalance

let account accountId (mailbox: Actor<_>) =
    let rec loop transactions = actor {
        let calculateBalance () = 
            let amount = 
                transactions 
                |> Map.toList
                |> List.sumBy (fun (_,Transaction(_, Money amount)) -> amount)
            printfn "Balance: %M" amount
            AccountBalance(accountId, Money amount)

        let! message = mailbox.Receive ()
        match message with
        | Deposit(transactionId, amount) -> 
            let transaction = Transaction(transactionId, amount)
            printfn "Deposit: %A" transaction
            return! loop (transactions |> Map.add transactionId transaction)
        | Withdraw(transactionId, Money amount) -> 
            let transaction = Transaction(transactionId, Money -amount)
            printfn "Withdraw: %A" transaction
            return! loop (transactions |> Map.add transactionId transaction)
        | QueryBalance -> 
            mailbox.Sender () <! calculateBalance () // this msg is sent to the deadletter
            return! loop transactions
    }
    loop Map.empty

let accountRef = spawn system "account" <| account (AccountId "acc1")
let deposit1 = Deposit(TransactionId "tx1", Money(100m))

accountRef <! deposit1
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Deposit(TransactionId "tx2", Money(20m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Withdraw(TransactionId "tx3", Money(50m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Deposit(TransactionId "tx4", Money(70m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Withdraw(TransactionId "tx5", Money(100m))
accountRef <! QueryBalance
accountRef <! deposit1
accountRef <! Deposit(TransactionId "tx6", Money(10m))
accountRef <! QueryBalance
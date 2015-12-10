##Messaging with Actors

##Sections

1. [Introduction](index.html)
2. **Messaging with Actors (Chapter 4)**
	- [Message Channel](#Message-Channel)
	- [Message](#Message)
3. Messaging Channels (Chapter 5)
4. Message Construction (Chapter 6)
5. Message Routing (Chapter 7)
6. Message Transformation (Chapter 8)
7. Message Endpoints (Chapter 9)
8. System Management and Infrastructure (Chapter 10)



##Message Channel

The Message Channel represents the way a consumer and a producer communicate. When using Akka.net, there is nothing special you need to do to implement this pattern as the actor's mailbox implements it for you. 

```fsharp
type ProcessorMessage = ProcessJob of int * int * int

let processor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! ProcessJob(x,y,z) = mailbox.Receive ()
        printfn "Processor: received ProcessJob %i %i %i" x y z
        return! loop ()
    }
    loop ()

let processorRef = spawn system "processor" processor

processorRef <! ProcessJob(1, 3, 5)
```

[Complete Code](https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/MessageChannel.fsx) - [Sections](#Sections)

##Message

Messages are sent between actors, they don't have any metadata or header (at least not from the actor point of view), they are just plain data and can be any F# type.

Example using multiple Primitive Types:

```fsharp
let scalarValuePrinter (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match box message with
        | :? string as msg -> printfn "ScalarValuePrinter: received String %s" msg
        | :? int as msg -> printfn "ScalarValuePrinter: received Int %i" msg
        | _ -> ()
        return! loop ()
    }
    loop ()
```

Example using Discriminated Unions:

```fsharp
type OrderProcessorCommand =
    | ExecuteBuyOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | ExecuteSellOrder of portfolioId: string * symbol: Symbol * quantity: int * price: Money

type OrderProcessorEvent =
    | BuyOrderExecuted of portfolioId: string * symbol: Symbol * quantity: int * price: Money
    | SellOrderExecuted of portfolioId: string * symbol: Symbol * quantity: int * price: Money

let orderProcessor (mailbox: Actor<_>) =
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        match message with
        | ExecuteBuyOrder(i, s, q, p) -> mailbox.Sender () <! BuyOrderExecuted(i, s, q, p)
        | ExecuteSellOrder(i, s, q, p) -> mailbox.Sender () <! SellOrderExecuted(i ,s, q, p)
        return! loop ()
    }
    loop ()
```

[Complete Code](https://github.com/jorgef/fsharpreactivepatterns/blob/master/MessagingWithActors/Message.fsx) - [Sections](#Sections)

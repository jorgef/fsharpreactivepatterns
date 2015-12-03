#Reactive Messaging Patterns with F# and Akka.NET

Recently I've been reading the [Reactive Messaging Patterns with the Actor Model](http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836) book by [Vaughn Vernon](https://twitter.com/vaughnvernon). This is a good book that applies the patterns described in the classic [Enterprise Integration Patterns](http://www.informit.com/store/enterprise-integration-patterns-designing-building-9780321200686) book using the Scala language and the Akka framework (Actor Model).

As I am an F# fan, I thought it would be a good exercise to translate the examples to F# and Akka.net. If you know F# and Akka.net (or want to learn), I encourage you to read the book and follow the examples I share here. Bear in mind that I won't cover in detail each pattern, I will just provide a short description and its F#/Akka.net example, for more details please refer to the book.

##Intro to Akka.NET F# API

Before we start, it is worth mentioning that Akka.net's F# API allows you to create actors in different ways, the simplest one allows you to do it in just one line of code!

```fsharp
let actorRef = spawn system "myActor" (actorOf (fun msg -> (* Handle message here *) () ))
```

Although that is very impressive, the truth is that sometimes you will need more control over the way the actor is created and how it interacts with Akka.net. So, to keep all the examples consistent I chose to use a more advanced technique, the "actor" computation expression:

```fsharp
let actor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        return! loop ()
    }
    loop ()
```

let actorRef = spawn system "myActor" actor

Although this second option is more verbose, it is also more powerful as you have access to the mailbox and you can control the actor's workflow (you will see in future examples).

As you can appreciate, an actor in F# is a function that:
	- Receives a mailbox as parameter
	- Returns an actor computation expression

The mailbox's type is Actor<'a>, where 'a is the type of message. In most of the cases you can leave it as Actor<_> as the F# compiler will infer the type for you.

The returned type, the actor computation expression, is implemented using a self-invoking recursive function. Its first line "let! message = mailbox.Receive ()" is receiving the message sent to the actor. if no message is available yet, the actor will be blocked until a message arrives. After the message is received and handled, the line "return! Loop ()" invokes the loop again to wait for the next message. The last line executes the loop function for the first time to initiate the actor. 

Don't worry if you couldn't follow the code for the first time, it took me a while to get my mind around. I recommend you to write a few actors to fully understand how it works.

Once you create an actor, you have a reference to it, so now you can send messages to it in this way:

```fsharp
actorRef <! "message"
```

The first 3 chapters of the book are basically about introducing Actor Model, Akka and Scala. The catalog really starts in chapter 4.

##List of Chapters

1. [Messaging with Actors (Chapter 4)](#messaging-with-actors-chapter-4)
2. Messaging Channels (Chapter 5)
3. Message Construction (Chapter 6)
4. Message Routing (Chapter 7)
5. Message Transformation (Chapter 8)
6. Message Endpoints (Chapter 9)
7. System Management and Infrastructure (Chapter 10)

##Messaging with Actors (Chapter 4)

###Message Channel

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

[see full code](https://github.com/jorgef/fsharpreactivepatterns/blob/master/Ch4/MessageChannel.fsx) - [go to chapters](#list-of-chapters)

###Message

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

[see full code](https://github.com/jorgef/fsharpreactivepatterns/blob/master/Ch4/Message.fsx) - [go to chapters](#list-of-chapters)

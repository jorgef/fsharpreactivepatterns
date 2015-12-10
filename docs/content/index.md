#Reactive Messaging Patterns with F# and Akka.NET

Recently I've been reading the [Reactive Messaging Patterns with the Actor Model](http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836) book by [Vaughn Vernon](https://twitter.com/vaughnvernon). This is a good book that applies the patterns described in the classic [Enterprise Integration Patterns](http://www.informit.com/store/enterprise-integration-patterns-designing-building-9780321200686) book using the Scala language and the Akka framework (Actor Model).

As I am an F# fan, I thought it would be a good exercise to translate the examples to F# and Akka.net. If you know F# and Akka.net (or want to learn), I encourage you to read the book and follow the examples I share here. Bear in mind that I won't cover in detail each pattern, I will just provide a short description and its F#/Akka.net example, for more details please refer to the book.

##Sections

1. **Introduction**
2. [Messaging with Actors (Chapter 4)](messaging-with-actors.html)
3. Messaging Channels (Chapter 5)
4. Message Construction (Chapter 6)
5. Message Routing (Chapter 7)
6. Message Transformation (Chapter 8)
7. Message Endpoints (Chapter 9)
8. System Management and Infrastructure (Chapter 10)

##Introduction

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

```fsharp
let actorRef = spawn system "myActor" actor
```

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

*This article is part of the <a href="https://sergeytihon.wordpress.com/2015/10/25/f-advent-calendar-in-english-2015"  target="_blank">F# Advent Calendar in English 2015</a> organized by <a href="https://twitter.com/sergey_tihon" target="_blank">Sergey Tihon</a>.*

Recently I've been reading the book <a href="http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">Reactive Messaging Patterns with the Actor Model</a> by <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a>. This book applies the patterns described in the <a href="http://www.informit.com/store/enterprise-integration-patterns-designing-building-9780321200686" target="_blank">Enterprise Integration Patterns</a> book using the <a href="http://www.scala-lang.org" target="_blank">Scala</a> language and the <a href="http://akka.io/" target="_blank">Akka</a> framework (Actor Model).

As I am an F# fan, I thought it would be good to translate the examples to <a href="http://fsharp.org" target="_blank">F#</a> and <a href="http://getakka.net" target="_blank">Akka.NET</a>. If you already know F# and Akka.NET (or want to learn), I encourage you to read the book and follow the examples I share here.

##Sections

1. **Introduction**
2. [Messaging with Actors](messaging-with-actors.html)
3. [Messaging Channels](messaging-channels.html)
4. [Message Construction](message-construction.html)
5. [Message Routing](message-routing.html)
6. [Message Transformation](message-transformation.html)
7. [Message Endpoints](message-endpoints.html)
8. [System Management and Infrastructure](system-management-and-infrastructure.html)

##Introduction

Before we start, it is worth mentioning that the <a href="http://getakka.net/docs/FSharp API" target="_blank">Akka.NET F# API</a> provides different ways to create actors, the simplest one allows you to do it in just one line of code!

```fsharp
let actorRef = spawn system "myActor" (actorOf (fun msg -> (* Handle message here *) () ))
```

Although this is very impressive, the truth is that sometimes you will need more control over the way the actor is created and how it interacts with Akka.NET. So, to keep the code examples consistent, I chose to use a more advanced technique, the "actor" computation expression:

```fsharp
let myActor (mailbox: Actor<_>) = 
    let rec loop () = actor {
        let! message = mailbox.Receive ()
        // Handle message here
        return! loop ()
    }
    loop ()
```

This second option is more verbose but it is also more powerful, as you have full access to the mailbox and you can control when the recursive function is executed. 

As you can see, an actor is just a function that:

- Receives the mailbox as parameter
- Returns an actor computation expression

The mailbox is of type **Actor<'a>**, where 'a is the type of message the actor will handle. In most cases you can leave the type as **Actor<_>** and the F# compiler will infer the right message type for you.

The returned type, the **actor** computation expression, is returned using a self-invoking recursive function called **loop**. Its first line **let! message = mailbox.Receive ()** is receiving the message sent to the actor. if no message is available yet, the actor will be blocked until a message arrives. After the message is received and handled, the line **return! Loop ()**  is executed, which invokes the loop again to wait for the next message. 

Finally, the last line **loop ()** executes the recursive function for the first time, starting the actor. 

Don't worry if you couldn't follow easily the code, it took me a while to get my mind around too. I recommend you to write a few actors to fully understand how it works.

Once you have defined an actor, you can create a new instance using the **spawn** function:

```fsharp
let actorRef = spawn system "myActor" myActor
```

Here we need to provide the actor's system, a unique name ("myActor") and the actor function (myActor). 

Now that you have created the actor, you can send messages to it in this way:

```fsharp
actorRef <! "message"
```

Of course you can send all types of messages, not just strings.

###How to Run the Examples

1. Clone *https://github.com/jorgef/fsharpreactivepatterns.git*
2. Open *FSharpReactivePatterns.sln* and build the solution (it will download the references)
3. Open the example (fsx file) you want to run
4. Select all the code but the part that sends the messages to the actor(s) and send it to the F# Interactive
5. Clear the F# Interactive (optional)
6. Select the code that sends the messages to the actor(s) and send it to the F# Interactive

<img src="img/run.gif" />

Great, now that you know the basics you can start browsing and running the different [patterns](#Sections), enjoy! 

And also let me know what you think: <a href="https://twitter.com/jorgefioranelli" target="_blank">@jorgefioranelli</a>

### Special Thanks

- To <a href="https://twitter.com/vaughnvernon" target="_blank">Vaughn Vernon</a> for writing the <a href=""http://www.informit.com/store/reactive-messaging-patterns-with-the-actor-model-applications-9780133846836" target="_blank">book</a>.
- To <a href="https://twitter.com/Horusiath" target="_blank">Bartosz Sypytkowski</a> for providing support about <a href="https://getakka.net" target="_blank">Akka.NET</a> and the <a href="https://getakka.net" target="_blank">F# API</a>.
- To <a href="https://twitter.com/sforkmann" target="_blank">Steffen Forkmann</a> for helping me to setup the documentation.
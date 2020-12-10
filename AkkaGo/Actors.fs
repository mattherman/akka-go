module Actors

open System.Text
open Akka.FSharp
open Akka.IO

type Command =
    | UserCommand of string

let dummyCommandActor (mailbox: Actor<'a>) msg  =
    let sender = mailbox.Sender ()
    match msg with
    | UserCommand cmd -> sender <! cmd

let connectionActor connection (mailbox: Actor<obj>) = 
    let commandActor = spawn mailbox.Context "command" (actorOf2 dummyCommandActor)
    let rec loop connection = actor {
        let! msg = mailbox.Receive()

        match msg with
        | :? Tcp.Received as received ->
            let data = (Encoding.ASCII.GetString (received.Data.ToArray())).Trim()
            commandActor <! UserCommand data
        | :? Tcp.Closed as closed ->
            // Not sure if this is working
            printf "Connection closed: %s" closed.Cause
        | :? Tcp.Aborted as aborted ->
            // Not sure if this is working
            printf "Connection aborted: %s" aborted.Cause
        | :? string as response ->
            connection <! Tcp.Write.Create (ByteString.FromString response)
        | _ -> mailbox.Unhandled()

        return! loop connection
    }

    loop connection

let serverActor address (mailbox: Actor<obj>) =
    let rec messageLoop() = actor {
        let! msg = mailbox.Receive()
        let sender = mailbox.Sender()
        
        match msg with
        | :? Tcp.Bound as bound ->
            printf "Listening on %O\n" bound.LocalAddress
        | :? Tcp.Connected as connected -> 
            printf "%O connected to the server\n" connected.RemoteAddress
            let connectionName = "connection_" + connected.RemoteAddress.ToString().Replace("[", "").Replace("]", "")
            let connectionRef = spawn mailbox.Context connectionName (connectionActor sender)
            sender <! Tcp.Register connectionRef
        | _ -> mailbox.Unhandled()

        return! messageLoop()
    }

    mailbox.Context.System.Tcp() <! Tcp.Bind(mailbox.Self, address)
    messageLoop()
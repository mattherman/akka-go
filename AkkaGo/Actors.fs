module Actors

open System.Text
open Akka.FSharp
open Akka.IO

open Messages
open Akka.Actor

let userActor username (mailbox: Actor<_>) =
    let rec receive () = actor {
        let! msg = mailbox.Receive ()
        mailbox.Unhandled msg
        return! receive ()
    }
    receive ()

let userCoordinatorActor (mailbox: Actor<_>) =
    let rec receive (users: Map<string, IActorRef>) = actor {
        let! msg = mailbox.Receive ()
        let sender = mailbox.Sender ()
        match msg with
        | AuthenticationRequest username ->
            if users |> Map.containsKey username then
                sender <! AuthenticationFailure UsernameUnavailable
                return! receive users
            else
                let userActorName = sprintf "user_%s" username
                let userActor = spawn mailbox.Context userActorName (userActor username)
                sender <! AuthenticationSuccess username
                return! receive (users.Add (username, userActor))
    }
    
    receive Map.empty

let commandProcessorActor connection (mailbox: Actor<obj>) =
    let rec unauthenticated () =
        actor {
            let! msg = mailbox.Receive ()
            let sender = mailbox.Sender ()

            match box msg with
            | :? string as username ->
                select "/user/userCoordinator" mailbox.Context.System <! AuthenticationRequest username
            | _ -> mailbox.Unhandled msg

            return! authenticating ()
        }
    and authenticating () =
        actor {
            let! msg = mailbox.Receive ()

            match msg with
            | :? AuthenticationResult as result ->
                match result with
                | AuthenticationSuccess username ->
                    connection <! sprintf "Successfully authenticated as %s!" username
                    return! authenticated username
                | AuthenticationFailure error ->
                    match error with
                    | UsernameUnavailable -> connection <! "Username is already in use." 
            | _ ->
                return! authenticating ()
        }
    and authenticated username =
        actor {
            let! msg = mailbox.Receive ()
            connection <! "OK"

            return! authenticated username
        }

    unauthenticated ()

let connectionActor connection (mailbox: Actor<obj>) = 
    let commandActor = spawn mailbox.Context "command" (commandProcessorActor mailbox.Self)
    let rec receive connection = actor {
        let! msg = mailbox.Receive ()

        match msg with
        | :? Tcp.Received as received ->
            let data = (Encoding.ASCII.GetString (received.Data.ToArray())).Trim()
            commandActor <! data
        | :? Tcp.Closed as closed ->
            // Not sure if this is working
            printf "Connection closed: %s" closed.Cause
        | :? Tcp.Aborted as aborted ->
            // Not sure if this is working
            printf "Connection aborted: %s" aborted.Cause
        | :? string as response ->
            connection <! Tcp.Write.Create (ByteString.FromString response)
        | _ -> mailbox.Unhandled ()

        return! receive connection
    }

    receive connection

let serverActor address (mailbox: Actor<obj>) =
    let rec receive () = actor {
        let! msg = mailbox.Receive ()
        let sender = mailbox.Sender ()
        
        match msg with
        | :? Tcp.Bound as bound ->
            printf "Listening on %O\n" bound.LocalAddress
        | :? Tcp.Connected as connected -> 
            printf "%O connected to the server\n" connected.RemoteAddress
            let connectionName = "connection_" + connected.RemoteAddress.ToString().Replace("[", "").Replace("]", "")
            let connectionRef = spawn mailbox.Context connectionName (connectionActor sender)
            sender <! Tcp.Register connectionRef
        | _ -> mailbox.Unhandled ()

        return! receive ()
    }

    mailbox.Context.System.Tcp () <! Tcp.Bind(mailbox.Self, address)
    receive ()
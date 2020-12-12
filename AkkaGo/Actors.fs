module Actors

open System.Text
open Akka.FSharp
open Akka.IO
open Akka.Actor

open Messages
open UserInterface
open System

let gameActor id subscribers playerOne (mailbox: Actor<_>) =
    let rec pending () = actor {
        let! msg = mailbox.Receive ()
        mailbox.Unhandled msg
        return! pending ()
    }
    pending ()

let gameCoordinatorActor (mailbox: Actor<_>) =
    let random = Random()
    let rec receive (games: Map<int, IActorRef>) = actor {
        let! msg = mailbox.Receive ()
        let sender = mailbox.Sender ()

        match msg with
        | CreateGame user ->
            let id = random.Next(10000, 100000)
            let gameActorName = sprintf "game_%i" id
            let game = spawn mailbox.Context gameActorName (gameActor id [sender] user)
            user <! JoinedGame game
            sender <! GameCreated id
            return! receive (games.Add (id, game))
        | _ -> mailbox.Unhandled msg
    }

    receive Map.empty

let userActor (mailbox: Actor<_>) =
    let rec waiting () = actor {
        let! msg = mailbox.Receive ()
        mailbox.Unhandled ()
        return! waiting ()
    }
    waiting ()
    // let rec unauthenticated () =
    //     actor {
    //         let! msg = mailbox.Receive ()
    //         let sender = mailbox.Sender ()
    //         match msg with
    //         | Authenticate username ->
    //             select "/user/userCoordinator" mailbox.Context.System <! Login (username, mailbox.Self)
    //         | _ ->
    //             mailbox.Unhandled msg
    //         return! authenticating sender
    //     }
    // and authenticating requestor =
    //     actor {
    //         let! msg = mailbox.Receive ()
    //         match msg with
    //         | AuthenticationResult result ->
    //             requestor <! result
    //             match result with
    //             | AuthenticationSuccess username ->
    //                 return! authenticated username
    //             | AuthenticationFailure error ->
    //                 return! unauthenticated ()
    //         | _ -> mailbox.Unhandled msg
    //     }
    // and authenticated username =
    //     actor {
    //         let! msg = mailbox.Receive ()
    //         mailbox.Unhandled msg
    //         return! authenticated username
    //     }
    // unauthenticated ()

let userCoordinatorActor (mailbox: Actor<_>) =
    let rec receive (authenticatedUsers: Map<string, IActorRef>) = actor {
        let! msg = mailbox.Receive ()
        let sender = mailbox.Sender ()
        match msg with
        | Login username ->
            if authenticatedUsers |> Map.containsKey username then
                sender <! AuthenticationFailure UsernameUnavailable
                return! receive authenticatedUsers
            else
                let userActorName = sprintf "user_%s" username
                let user = spawn mailbox.Context userActorName userActor
                sender <! AuthenticationSuccess (username, user)
                return! receive (authenticatedUsers.Add (username, user))
        | Logout username ->
            return! receive (authenticatedUsers.Remove username)
    }
    
    receive Map.empty

let commandProcessorActor userInterface (mailbox: Actor<obj>) =
    let rec unauthenticated () =
        userInterface <! authenticationPrompt
        actor {
            let! msg = mailbox.Receive ()

            match msg with
            | :? string as username ->
                select "/user/userCoordinator" mailbox.Context.System <! Login username
                return! authenticating ()
            | _ -> mailbox.Unhandled msg

            return! unauthenticated ()
        }
    and authenticating () =
        actor {
            let! msg = mailbox.Receive ()
            match msg with
            | :? AuthenticationResult as authResult ->
                match authResult with
                | AuthenticationSuccess (username, user) ->
                    userInterface <! sprintf "Welcome, %s!\n" username
                    return! mainMenu (username, user)
                | AuthenticationFailure error ->
                    match error with
                    | UsernameUnavailable ->
                        userInterface <! "Username is already in use. Please try again.\n"
                        return! unauthenticated ()
            | _ -> mailbox.Unhandled msg
            
            return! authenticating ()
        }
    and mainMenu (username, user) =
        userInterface <! helpMenu
        userInterface <! "> "
        actor {
            let! msg = mailbox.Receive ()
            let (|StartGame|JoinGame|ListGames|Help|) (str:string) =
                match str.ToLower () with
                | "start" -> StartGame
                | "join" -> JoinGame
                | "list" -> ListGames
                | _ -> Help

            match msg with
            | :? string as input ->
                match input with
                | StartGame ->
                    select "/user/gameCoordinator" mailbox.Context.System <! CreateGame user
                | _ ->
                    userInterface <! helpMenu
            | :? GameCommand as gameCmd ->
                match gameCmd with
                | GameCreated id ->
                    return! game id
                | _ ->
                    mailbox.Unhandled msg
            | _ ->
                mailbox.Unhandled msg

            return! mainMenu (username, user)
        }
    and game gameId =
        userInterface <! "> "
        actor {
            let! msg = mailbox.Receive ()
            userInterface <! "OK\n"
            return! game gameId
        }

    userInterface <! logo
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
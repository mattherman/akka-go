module Messages

open Akka.Actor

type UserAuthenticationCommand =
    | Login of username: string
    | Logout of username: string

type AuthenticationError =
    | UsernameUnavailable

type AuthenticationResult =
    | AuthenticationSuccess of username: string * user: IActorRef
    | AuthenticationFailure of AuthenticationError

type GameCommand =
    | CreateGame of user: IActorRef
    | GameCreated of id: int
    | JoinGame of user: IActorRef * gameId: int
    | GameJoined of id: int

type UserCommand =
    | Authenticate of username: string
    | AuthenticationResult of AuthenticationResult
    | StartGame
    | JoinedGame of game: IActorRef

type UserInput = UserInput of string

type UserInterfaceCommand =
    | Input of string
    | Output of string
    | AcceptInput
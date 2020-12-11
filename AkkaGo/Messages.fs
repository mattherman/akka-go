module Messages

open System

type AuthenticationRequest = AuthenticationRequest of string

type AuthenticationError =
    | UsernameUnavailable

type AuthenticationResult =
    | AuthenticationSuccess of username: string
    | AuthenticationFailure of AuthenticationError

type NewGame = NewGame of username: string
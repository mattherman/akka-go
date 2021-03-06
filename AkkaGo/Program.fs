open System
open System.Net
open Akka
open Akka.FSharp

open Actors

[<EntryPoint>]
let main argv =
    let system = System.create "system" (Configuration.defaultConfig())
    let address = IPEndPoint(IPAddress.Any, 9090)
    let userCoordinator = spawn system "userCoordinator" userCoordinatorActor
    let gameCoordinator = spawn system "gameCoordinator" gameCoordinatorActor
    let server = spawn system "server" (serverActor address)
    system.WhenTerminated.Wait ()
    0 // return an integer exit code
open System
open System.Net
open Akka
open Akka.FSharp

open Actors

[<EntryPoint>]
let main argv =
    let system = System.create "system" (Configuration.defaultConfig())
    let address = IPEndPoint(IPAddress.Any, 9090)
    let server = spawn system "server" (serverActor address)
    system.WhenTerminated.Wait ()
    0 // return an integer exit code
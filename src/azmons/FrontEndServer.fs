module FrontEndServer

open AsyncExtensions
open FSharp.Interop.Dynamic
open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open Nancy
open System

type IndexModule() as x =
    inherit NancyModule()
    do
     x.Get.["/"] <- fun _ -> box x.View.["index"]

type LoggingDisposable(d: IDisposable) =
    interface IDisposable with
        member x.Dispose() =
            printfn "IT'S DEAD JIM"
            d.Dispose()
let logit d = new LoggingDisposable(d)

[<HubName("display")>]
type DisplayHub() =
    inherit Hub()
    override x.OnConnected() =
        printfn "Adding %s to display group" x.Context.ConnectionId
        x.Groups.Add(x.Context.ConnectionId, "display") |> awaitTask |> ignore
        base.OnConnected()
    override x.OnDisconnected(stopCalled) =
        x.Groups.Remove(x.Context.ConnectionId, "display") |> awaitTask |> ignore
        base.OnDisconnected(stopCalled)

type Publisher(src: IObservable<_>, clients: IHubConnectionContext<_>) as this =
    let events = Observable.subscribe (fun s -> this.Publish s) src |> logit
    member __.Clients with get() = clients
    member x.Publish s =
        printfn "Publishing! %s" s
        try
            x.Clients.Group("display")?event(s)
        with
        | :? Exception as e ->
            printfn "Oh noes, got a %s" (e.ToString())
    interface IDisposable with
        member x.Dispose() =
            (events :> IDisposable).Dispose()

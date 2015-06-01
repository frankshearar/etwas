module FrontEndServer

open AsyncExtensions
open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open System
open System.Reactive.Subjects

let sendAll msg =
    async {
        GlobalHost.ConnectionManager.GetConnectionContext<_>().Connection.Broadcast msg
        |> awaitTask
        |> ignore
    } |> Async.RunSynchronously

[<HubName("display")>]
type DisplayHub(subject: Subject<string>) =
    inherit Hub()
    // I don't want to expose events, but I can't figure out how to make
    // a private variable that's still accessible in Dispose(). It's _supposed_
    // to just happen automatically.
    let events = Observable.subscribe (fun s -> sendAll s) subject
    member x.Events with get() = events
    with
    interface IDisposable with
        member x.Dispose() =
            x.Events.Dispose()
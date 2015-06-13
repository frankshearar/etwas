module FrontEndServer

open AsyncExtensions
open FSharp.Interop.Dynamic
open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open Nancy
open System
open System.Reactive.Subjects
open System.Threading

type IndexModule() as x =
    inherit NancyModule()
    do
     x.Get.["/"] <- fun _ -> box x.View.["index"]

let msgCount = ref 0L

let printMessagesPerSecond n =
    printfn "%d\tmsg/s" n

let publishMessageRate = async {
    while true do
        do! Async.Sleep(1000)
        printMessagesPerSecond (Interlocked.Exchange(msgCount, 0L))
}

Async.Start(publishMessageRate)

[<HubName("display")>]
type DisplayHub() =
    inherit Hub()
    override x.OnConnected() =
        printfn "Adding %s to display group" x.Context.ConnectionId
        x.Groups.Add(x.Context.ConnectionId, "display") |> awaitTask |> ignore
        base.OnConnected()
    override x.OnDisconnected(stopCalled) =
        printfn "Removing %s from display group" x.Context.ConnectionId
        x.Groups.Remove(x.Context.ConnectionId, "display") |> awaitTask |> ignore
        base.OnDisconnected(stopCalled)

type Publisher(src: Subject<string>, clients: IHubConnectionContext<_>) as this =
    let events      = src |> Observable.subscribe this.Publish
    let countEvents = src |> Observable.subscribe (fun _ -> Interlocked.Increment(msgCount) |> ignore)
    member __.Publish s =
        try
            clients.Group("display")?event(s)
        with
        | _ as e ->
            printfn "Exception publishing event: %s" (e.ToString())
    interface IDisposable with
        member __.Dispose() =
            events.Dispose()

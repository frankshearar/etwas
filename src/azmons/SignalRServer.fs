module SignalRServer

open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open System.Reactive.Subjects

let observedEvents = new Subject<string>()

let wat = observedEvents |> Observable.subscribe (fun s -> printfn "Got a %s" s)

[<HubName("event")>]
type EventHub() =
    inherit Hub()
    member __.event(xml) =
        observedEvents.OnNext(xml)
        box ""

module SignalRServer

open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open System.Reactive.Subjects

let observedEvents = new Subject<string>()

[<HubName("event")>]
type EventHub() =
    inherit Hub()
    member __.event(xml) =
        observedEvents.OnNext(xml)
        box ""

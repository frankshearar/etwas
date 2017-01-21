module SignalRServer

open Counter
open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open System.Reactive.Subjects

let observedEvents = new Subject<string>()
let receiveCounter = Counter.createCounter "Server Receive messages/second"

[<HubName("event")>]
type EventHub() =
    inherit Hub()
    member __.event(xml) =
        receiveCounter |> Counter.fireCounter
        observedEvents.OnNext(xml)
        box ""

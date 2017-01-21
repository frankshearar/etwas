module Ping

open System.Diagnostics.Tracing

[<EventSource(Name = "Ping")>]
type PingEventSource() =
    inherit EventSource()
    [<Event(2000)>]
    member __.Ping() =
        __.WriteEvent(2000)

// An EventSource _really really_ wants to be a Singleton.
let ping = new PingEventSource()
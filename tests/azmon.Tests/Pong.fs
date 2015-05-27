module Pong

open System.Diagnostics.Tracing

[<EventSource(Name = "Pong")>]
type PongEventSource() =
    inherit EventSource()
    [<Event(2001)>]
    member __.Pong() =
        __.WriteEvent(2001)

// An EventSource _really really_ wants to be a Singleton.
let pong = new PongEventSource()
module Publish

open Microsoft.Diagnostics.Tracing
open System

let http url (evt: TraceEvent) =
    () // something

let stdout (evt: TraceEvent) =
    printfn "%A" (evt.ToString())

// Map a name to a function that accepts a TraceEvent
let resolveSink (name: string): (TraceEvent -> unit) =
    if name.StartsWith("http") || name.StartsWith("https") then
        http name
    else if name.ToLower() = "stdout" then
        stdout
    else
        ignore

// Configure publishers, and publish all events observed.
let start names (subject: IObservable<TraceEvent>) =
    let sinks = match names with
                | [] -> [stdout]
                | xs -> xs
                        |> List.map resolveSink

    sinks
    |> List.iter (fun sink -> Observable.add sink subject)
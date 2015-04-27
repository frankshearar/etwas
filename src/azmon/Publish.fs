module Publish

open Microsoft.Practices.EnterpriseLibrary.SemanticLogging
open Microsoft.Diagnostics.Tracing
open System

let stdout (evt: TraceEvent) =
    printfn "%A" (evt.ToString())

// Configure publishers, and publish all events observed.
let start names (subject: IObservable<TraceEvent>) =
    Observable.add stdout subject
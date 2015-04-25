module Publish

open Microsoft.Practices.EnterpriseLibrary.SemanticLogging
open Microsoft.Diagnostics.Tracing
open System

let stdout (evt: TraceEvent) =
    printfn "%A" (evt.ToString())

let start names (subject: IObservable<TraceEvent>) =
    Observable.add stdout subject
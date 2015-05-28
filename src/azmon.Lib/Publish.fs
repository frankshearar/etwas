module Publish

open AsyncExtensions
open Microsoft.AspNet.SignalR.Client
open Microsoft.Diagnostics.Tracing
open System

type Session = {
                 Sinks: Map<string,TraceEvent->unit>
                 HttpSinks: IHubProxy list
                 ToStdout: bool
                 Observers: IDisposable list
               }
    with
    interface IDisposable with
        member x.Dispose() =
            x.Observers
            |> List.iter (fun d -> d.Dispose())

let newSession = {Sinks = Map.empty; HttpSinks = List.empty; ToStdout = false; Observers = List.empty}

let configuration = function
    | {Sinks = sinks} -> sinks |> Map.toList |> List.map fst

let http (hub: IHubProxy) =
    fun (evt: TraceEvent) ->
        doItNow(hub.Invoke("event", evt.EventName))

let stdout (evt: TraceEvent): unit =
    printfn "%A" (evt.ToString())

let private connectToSignalr url =
    let connection = new HubConnection(url)
    let hub = connection.CreateHubProxy("event")
    doItNow(connection.Start())
    hub

// Map a name to a function that accepts a TraceEvent
let private resolveSink (name: string) session =
    if name.StartsWith("http") || name.StartsWith("https") then
        let hub = connectToSignalr name
        let newPub = http hub
        {session with Sinks = Map.add name newPub session.Sinks; HttpSinks = hub :: session.HttpSinks}
    else if name.ToLower() = "stdout" then
        {session with ToStdout = true}
    else
        session

// Return a PublishSession that has subscribed a callback to each
// (recognised) name in names.
let start names (subject: IObservable<TraceEvent>) =
    printfn "Publishing started"
    let keys map =
        map
        |> Map.toList
        |> List.map snd
    let session  = if List.isEmpty names then ["stdout"] else names
                   |> Seq.ofList
                   |> Seq.distinct
                   |> Seq.fold (fun session each -> resolveSink each session) newSession
    let httpObservers = (keys session.Sinks)
                        |> List.map (fun sink -> Observable.subscribe sink subject)
    let otherObservers = if session.ToStdout then [Observable.subscribe stdout subject] else []
    {session with Observers = (List.append httpObservers otherObservers)}

let stop (session: Session) =
    printfn "Publishing stopped"
    (session :> IDisposable).Dispose()
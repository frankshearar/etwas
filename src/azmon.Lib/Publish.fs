module Publish

open AsyncExtensions
open Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
open Microsoft.AspNet.SignalR.Client
open Microsoft.Diagnostics.Tracing
open System
open System.Threading
open System.Threading.Tasks.Dataflow

type Session = {
                 Sinks: Map<string,TraceEvent->unit>
                 HttpSinks: IDisposable list
                 TableSinks: WindowsAzureTableSink list
                 ToStdout: bool
                 Observers: IDisposable list
               }
    with
    interface IDisposable with
        member x.Dispose() =
            x.HttpSinks |> List.iter (fun s -> s.Dispose())
            x.Observers |> List.iter (fun d -> d.Dispose())

let newSession = {Sinks = Map.empty; HttpSinks = List.empty; TableSinks = List.empty; ToStdout = false; Observers = List.empty}

let configuration = function
    | {Sinks = sinks} -> sinks |> Map.toList |> List.map fst

let private serialize (evt: TraceEvent) =
    evt.ToString()

let azureTable (sink: WindowsAzureTableSink) =
    AzureTableSink.translateToInProc >> sink.OnNext

let http (url: string) =
    let connection = new HubConnection(url)
    let hub = connection.CreateHubProxy("event")
    connection.add_ConnectionSlow (fun () -> printfn ">>> connection slow: %s"   url)
    connection.add_Closed         (fun () -> printfn ">>> connection closed: %s" url)
    connection.add_Reconnecting   (fun () -> printfn ">>> reconnecting: %s"      url)
    connection.add_Reconnected    (fun () -> printfn ">>> reconnected: %s"       url)
    connection.add_Error          (fun e  -> printfn ">>> error: %s :\n>>> %s"   url (e.ToString()))
    let buffer = new BufferBlock<_>()

    let firstTime = ref true
    let sendEvent (serialisedEvent: string) =
        if !firstTime then
            printfn ">>> Connecting for the first time"
            // The connection _should_ handle reconnections, so we only need to kick things off once.
            firstTime := false
            doItNow(connection.Start()) // Need to handle connection failures better.
        printfn ">>> Sending event"
        try
            doItNow(hub.Invoke("event", serialisedEvent))
        with
        | e -> printfn "Boom: %s" (e.ToString())

    let wat = {new IObserver<_> with
                        member __.OnNext(_) = printfn "Next"
                        member __.OnError(e) = printfn "Error: %A" e
                        member __.OnCompleted() = printfn "Completed"}
    let events = buffer.AsObservable()
    events.Subscribe wat |> ignore

    let sendToBuffer = Observable.subscribe sendEvent events

    let sink = fun (evt: TraceEvent) ->
                    // Diagnostics "aggressively reuses" TraceEvent instances, and cloning is more
                    // expensive than just reading out the data. We could write these data out to
                    // our own type, but we have to serialize sooner or later anyway...
                    buffer.Post((serialize evt)) |> ignore
    sendToBuffer, sink

let stdout (evt: TraceEvent): unit =
    printfn "%s" (serialize evt)

// Remove our custom "TableName=thing" value from a normal Azure connection string.
let connectionStringFrom (s: string) =
    let pieces = s.Split([|";"|], StringSplitOptions.RemoveEmptyEntries)
    pieces
    |> Array.filter (fun v -> not(v.ToLower().StartsWith("tablename")))
    |> String.concat ";"

// Given a string like "Key=value;Key2=other;TableName=thing", return "thing"
let tableNameFrom (s: string) =
    let pieces = s.Split([|";"|], StringSplitOptions.RemoveEmptyEntries)
    let keyValues = pieces |> Array.filter (fun v -> v.ToLower().StartsWith("tablename"))
    let keyValue = Array.get keyValues 0
    let bits = keyValue.Split([|'='|])
    Array.get bits 1

// Map a name to a function that accepts a TraceEvent
let private resolveSink (name: string) session =
    if name.StartsWith("http") || name.StartsWith("https") then
        let connection, sink = http name
        {session with Sinks = Map.add name sink session.Sinks; HttpSinks = connection :: session.HttpSinks}
    else if name.StartsWith("azure") then
        let parts = name.Split([|':'|])
        if parts.Length < 2 then invalidArg "name" (sprintf "Not a valid Azure table sink name: %s" name)
        let tableName = tableNameFrom (parts.[1])
        let connectionString = connectionStringFrom (parts.[1])
        let sink = new WindowsAzureTableSink("instanceName", connectionString, tableName, TimeSpan.FromSeconds(1.0), 2000, Timeout.InfiniteTimeSpan)
        let newPub = azureTable sink
        {session with Sinks = Map.add name newPub session.Sinks; TableSinks = sink :: session.TableSinks}
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
    let sinkObservers = (keys session.Sinks)
                        |> List.map (fun sink -> Observable.subscribe sink subject)
    let otherObservers = if session.ToStdout then [Observable.subscribe stdout subject] else []
    {session with Observers = List.append sinkObservers otherObservers}

let stop (session: Session) =
    printfn "Publishing stopped"
    (session :> IDisposable).Dispose()
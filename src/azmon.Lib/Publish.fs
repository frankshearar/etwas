module Publish

open AsyncExtensions
open Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
open Microsoft.AspNet.SignalR.Client
open Microsoft.Diagnostics.Tracing
open System
open System.Threading

type Session = {
                 Sinks: Map<string,TraceEvent->unit>
                 HttpSinks: HubConnection list
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

let http (hub: IHubProxy) =
    fun (evt: TraceEvent) ->
        doItNow(hub.Invoke("event", (serialize evt)))

let stdout (evt: TraceEvent): unit =
    printfn "%s" (serialize evt)

let private connectToSignalr url =
    let connection = new HubConnection(url)
    let hub = connection.CreateHubProxy("event")
    doItNow(connection.Start())
    connection, hub

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
        let connection, hub = connectToSignalr name
        let newPub = http hub
        {session with Sinks = Map.add name newPub session.Sinks; HttpSinks = connection :: session.HttpSinks}
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
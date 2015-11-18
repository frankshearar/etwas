// "Fan out" events to all configured data sinks.
// We always print errors to stdout.
// We usually print rare events like reconnections to SignalR sinks.
// We only print other diagnostic information when we pass printDebug = true
// to Publish.start.
//
// Basic architecture: the user supplies the names of data sinks.
// resolveSink turns these names into (TraceEvent -> unit) functions that, when given
// a TraceEvent, sends the data wherever it needs to go. Given no sinks, we publish
// only to stdout.
module Publish

open AsyncExtensions
open Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks
open Microsoft.AspNet.SignalR.Client
open Microsoft.Diagnostics.Tracing
open Microsoft.WindowsAzure.ServiceRuntime
open System
open System.Diagnostics
open System.Threading
open System.Threading.Tasks.Dataflow

type Session = {
                 Sinks: Map<string,TraceEvent->unit>     // Map the names in the session to their sink functions.
                 HttpSinks: IDisposable list             // | Tracking the Sinks separately lets us test that
                 TableSinks: WindowsAzureTableSink list  // | deduplication of sinks happens.
                 Observers: IDisposable list             // |
                 ToStdout: bool
               }
    with
    interface IDisposable with
        member x.Dispose() =
            x.HttpSinks  |> List.iter (fun s -> s.Dispose())
            x.Observers  |> List.iter (fun d -> d.Dispose())
            x.TableSinks |> List.iter (fun t -> t.Dispose())

let newSession = {Sinks = Map.empty; HttpSinks = List.empty; TableSinks = List.empty; ToStdout = false; Observers = List.empty}

let configuration = function
    | {Sinks = sinks} -> sinks |> Map.toList |> List.map fst

let private serialize (evt: TraceEvent) =
    evt.ToString()

let azureTable (sink: WindowsAzureTableSink) =
    AzureTableSink.translateToInProc >> sink.OnNext

let private debug printDebug s = if printDebug then printfn s

// Return a function that, when run, will repeatedly try connect
// (at 1 attempt/second) to a SignalR server.
let connectWithRetry (printDebug: bool) =
    let firstTime = ref true
    fun (connection: Connection) ->
        async {
            if !firstTime then
                debug printDebug ">>> Connecting for the first time"
                // The connection _should_ handle reconnections, so we only need to kick things off once.
                firstTime := false
                let running = ref false
                while not !running do
                    try
                        do! connection.Start() |> awaitTask
                        running := true
                    with
                    | :? AggregateException as e ->
                        do! Async.Sleep 1000
                        printfn "Error connecting; retrying: %s" (e.ToString())
        }

// Tracks buffered messages across ALL HTTP sinks. Ideally we'd break these
// out into separate counters, but then we need to define the counters
// separately, and we'd need to have --install-counters know how many sinks
// we have. In the case of a role: sink, we cannot know until runtime how
// many sinks we'd need...
let totalBufferSize = ref 0L

let http (printDebug: bool) (url: string) =
    let connection = new HubConnection(url)
    connection.add_ConnectionSlow (fun () -> printfn ">>> connection slow: %s"   url)
    connection.add_Closed         (fun () -> printfn ">>> connection closed: %s" url)
    connection.add_Reconnecting   (fun () -> printfn ">>> reconnecting: %s"      url)
    connection.add_Reconnected    (fun () -> printfn ">>> reconnected: %s"       url)
    connection.add_Error          (fun e  -> printfn ">>> error: %s :\n>>> %s"   url (e.ToString()))
    let hub = connection.CreateHubProxy("event")
    let buffer = new BufferBlock<_>()

    // --> BufferBlock
    let sink = fun (evt: TraceEvent) ->
                    Interlocked.Increment(totalBufferSize) |> ignore
                    // Diagnostics "aggressively reuses" TraceEvent instances, and cloning is more
                    // expensive than just reading out the data. We could write these data out to
                    // our own type, but we have to serialize sooner or later anyway...
                    buffer.Post((serialize evt)) |> ignore

    // BufferBlock -->
    let connect = connectWithRetry printDebug
    let sendEvent (serialisedEvent: string) =
        async {
            do! connect(connection)

            debug printDebug ">>> Sending event"
            try
                do! hub.Invoke("event", serialisedEvent) |> awaitTask
            with
            | e -> printfn "Boom: %s" (e.ToString())
        } |> Async.RunSynchronously
        Interlocked.Decrement(totalBufferSize) |> ignore

    let outOfBlock = buffer.AsObservable()
    if printDebug then
        let observeToConsole = {new IObserver<_> with
                                 member __.OnNext(_)     = printfn "Next"
                                 member __.OnError(e)    = printfn "Error: %s" (e.ToString())
                                 member __.OnCompleted() = printfn "Completed"}
        outOfBlock.Subscribe observeToConsole |> ignore

    let sendToBuffer = Observable.subscribe sendEvent outOfBlock
    sendToBuffer, sink

let stdout (evt: TraceEvent): unit =
    printfn "Event: %d%s" (int evt.ID) (if evt.EventName = "" then "" else sprintf " (%s)" evt.EventName)
    printfn "Timestamp: %s" (evt.TimeStamp.ToString("o"))
    printfn "Task: %s" evt.TaskName
    printfn "ActivityId: %s" (evt.ActivityID.ToString())
    printfn "Message: '%s'" evt.FormattedMessage
    evt.PayloadNames
    |> Array.map (fun name -> name, (evt.PayloadIndex(name)))
    |> Array.map (fun (name, idx) -> name, evt.PayloadString(idx))
    |> Array.map (fun (name, value) -> sprintf "%s = %s" name value)
    |> String.concat "\n"
    |> printfn "%s"
    //printfn "%s" (serialize evt)

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
    if Array.isEmpty keyValues then
        "Azmon" // Default to an egotistical name.
    else
        let keyValue = Array.get keyValues 0
        let bits = keyValue.Split([|'='|])
        Array.get bits 1

// Map a name to a function that accepts a TraceEvent, and return a session that
// contains this function.
let private resolveSink (printDebug : bool) (name: string) session =
    let resolveHttp printDebug name session =
        let connection, sink = http printDebug name
        {session with Sinks = Map.add name sink session.Sinks; HttpSinks = connection :: session.HttpSinks}
    if name.StartsWith("http") || name.StartsWith("https") then
        resolveHttp printDebug name session
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
let start names (counterMaker: string -> PerformanceCounter option) (printDebug: bool) (subject: IObservable<TraceEvent>) =
    let publishPerf = async {
        let bufCount = counterMaker "Total publishing buffer size"
        while true do
            do! Async.Sleep 500 // Means in perfmon you don't get a "wiggle" where 2 secs pass before an update.
            !totalBufferSize |> Counter.setCounter bufCount
    }
    publishPerf |> Async.Start

    printfn "Publishing started"
    let keys map =
        map
        |> Map.toList
        |> List.map snd
    let session  = if List.isEmpty names then ["stdout"] else names
                   |> Seq.ofList
                   |> Seq.distinct
                   |> Seq.fold (fun session each -> resolveSink printDebug each session) newSession
    let sinkObservers = (keys session.Sinks)
                        |> List.map (fun sink -> Observable.subscribe sink subject)
    let otherObservers = if session.ToStdout then [Observable.subscribe stdout subject] else []
    {session with Observers = List.append sinkObservers otherObservers}

let stop (session: Session) =
    printfn "Publishing stopped"
    (session :> IDisposable).Dispose()
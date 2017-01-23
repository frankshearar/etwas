module PublishTests

open AsyncExtensions
open Microsoft.AspNet.SignalR.Client
open Microsoft.Diagnostics.Tracing
open Microsoft.Owin.Hosting
open NUnit.Framework
open System
open System.Reactive.Subjects
open System.Threading.Tasks

type Server = {
                Server: IDisposable
                Uri: string
              }
    with
    interface IDisposable with
        member x.Dispose() =
            x.Server.Dispose()

let uniqueName() =
    System.Guid.NewGuid.ToString()

let nextPort = new Random()
let getServer() =
    lock nextPort (fun () ->
        let mutable port = 0
        let mutable openPort = false
        let mutable server: IDisposable = Unchecked.defaultof<IDisposable>
        let mutable uri = ""
        while not(openPort) do
            port <- (nextPort.Next 10000) + 10000 // Random port between 10k and 20k
            uri <- sprintf "http://localhost:%d/" port
            try
                server <- WebApp.Start<Bootstrapper.Startup>(uri)
                openPort <- true
            with
            | :? System.Reflection.TargetInvocationException -> () // No, really. It contains a System.Net.HttpListenerException
        {Server = server; Uri = uri})

type ThunkDelegate = delegate of unit -> unit

type BlankTraceEvent() =
    inherit TraceEvent(0, 0, "Fake task", Guid.Empty, 0, "", Guid.Empty, "")
    static member private Empty() = ()
    override x.PayloadNames with get() = [|"fake"|]
    override x.PayloadValue(index) = "fake" :> obj
    override x.Target
        with get() = new ThunkDelegate(BlankTraceEvent.Empty) :> Delegate
        and set(_) = ()

// TODO: Test has rotted - need to restore!
//[<Test>]
//let ``http publisher publishes``() =
//    use server = getServer()
//    let spotted = new TaskCompletionSource<bool>(false)
//    use events = Observable.subscribe (fun _ -> spotted.TrySetResult(true) |> ignore) SignalRServer.observedEvents
//    use monitoring = Monitor.start (uniqueName()) [Ping.PingEventSource.GetName(typeof<Ping.PingEventSource>)]
//    use connection = new HubConnection(server.Uri)
//    let hub = connection.CreateHubProxy("event")
//    let registeredHttpSink = Publish.http [|server.Uri|]
//    async {
//        do! connection.Start() |> awaitTask
//        registeredHttpSink (new BlankTraceEvent())
//
//        do! spotted.Task |> awaitTask
//    }
//    |> Async.CancelAfterWithCleanup 2000 (fun () -> spotted.TrySetResult(false) |> ignore)
//    |> Async.RunSynchronously
//    |> ignore
//    Assert.That(spotted.Task.IsCompleted)
//    Assert.That(spotted.Task.Result)

// 1-adic Constantly
let constantly x =
    fun _ -> x

[<Test>]
let ``Publish.start [] logs to stdout``() =
    use ignored = new Subject<_>()
    use session = Publish.start [] (constantly None) true ignored
    Assert.That(session.ToStdout)
    Assert.That(true, "Actual functionality covered by the ConsoleTests")

[<Test>]
let ``HTTP sinks automatically dedupe``() =
    use server = getServer()
    use ignored = new Subject<_>()
    use session = Publish.start [server.Uri; server.Uri] (constantly None) true ignored
    Assert.AreEqual(1, List.length session.HttpSinks)

// Test has rotted - need to restore!
//[<Test>]
//let ``Publish.stop disconnects HTTP sinks``() =
//    use server = getServer()
//    use ignored = new Subject<_>()
//    use session = Publish.start [server.Uri] ignored
//    let closed = ref false
//    session.HttpSinks |> List.iter (fun s -> s.add_Closed (fun () -> closed := true))
//    Publish.stop session
//    Assert.That(!closed)

open Microsoft.AspNet.SignalR.Client
[<Test>]
let ``SignalR server works``() =
    use server = getServer()
    let spotted = new TaskCompletionSource<bool>(false)
    use events = Observable.subscribe (fun _ -> spotted.TrySetResult(true) |> ignore) SignalRServer.observedEvents
    use connection = new HubConnection(server.Uri)
    let hub = connection.CreateHubProxy("event")
    let s = "serialised event"
    async {
        do! connection.Start() |> awaitTask
        do! hub.Invoke("event", s) |> awaitTask
        do! spotted.Task |> awaitTask
    }
    |> Async.CancelAfterWithCleanup 5000  (fun () -> spotted.TrySetResult(false) |> ignore)
    |> Async.RunSynchronously
    |> ignore
    Assert.That(spotted.Task.Result)

[<TestCase("DefaultEndpointsProtocol=https;AccountName=storageaccountname;AccountKey=fakebase64;TableName=tablename")>]
[<TestCase("DefaultEndpointsProtocol=https;AccountName=storageaccountname;TableName=tablename;AccountKey=fakebase64")>]
[<TestCase("TableName=tablename;DefaultEndpointsProtocol=https;AccountName=storageaccountname;AccountKey=fakebase64")>]
let ``connectionStringFrom parses out connection string``(name) =
    Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=storageaccountname;AccountKey=fakebase64", (Publish.connectionStringFrom name))

[<TestCase("DefaultEndpointsProtocol=https;AccountName=storageaccountname;AccountKey=fakebase64;TableName=tablename")>]
[<TestCase("DefaultEndpointsProtocol=https;AccountName=storageaccountname;TableName=tablename;AccountKey=fakebase64")>]
[<TestCase("TableName=tablename;DefaultEndpointsProtocol=https;AccountName=storageaccountname;AccountKey=fakebase64")>]
let ``tableNamefrom parses out table name``(name) =
    Assert.AreEqual("tablename", (Publish.tableNameFrom name))

[<TestCase("azureDefaultEndpointsProtocol=https;AccountName=storageaccountname;AccountKey=fakebase64;TableName=tablename")>]
[<TestCase("azure")>]
let ``resolveSinkFailsForInvalidAzureName``(brokenName) =
    Assert.Throws<ArgumentException>(fun () -> Publish.start [brokenName] (constantly None) true (new Subject<_>()) |> ignore) |> ignore

[<TestCase("azure:UseDevelopmentStorage=true;TableName=tablename")>]
let ``resolveSinkPassesForValidAzureName``(workingName) =
    Assert.DoesNotThrow(fun () -> Publish.start [workingName] (constantly None) true (new Subject<_>()) |> ignore)
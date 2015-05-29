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
                server <- WebApp.Start<SignalRServer.Startup>(uri)
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

[<Test>]
let ``http publisher publishes``() =
    use server = getServer()
    let spotted = new TaskCompletionSource<bool>(false)
    use events = Observable.subscribe (fun _ -> spotted.TrySetResult(true) |> ignore) SignalRServer.observedEvents
    use monitoring = Monitor.start (uniqueName()) [Ping.PingEventSource.GetName(typeof<Ping.PingEventSource>)]
    use connection = new HubConnection(server.Uri)
    let hub = connection.CreateHubProxy("event")
    let registeredHttpSink = Publish.http hub
    async {
        do! connection.Start() |> awaitTask
        registeredHttpSink (new BlankTraceEvent())

        do! spotted.Task |> awaitTask
    }
    |> Async.CancelAfterWithCleanup 2000 (fun () -> spotted.TrySetResult(false) |> ignore)
    |> Async.RunSynchronously
    |> ignore
    Assert.That(spotted.Task.IsCompleted)
    Assert.That(spotted.Task.Result)

[<Test>]
let ``Publish.start [] logs to stdout``() =
    use ignored = new Subject<_>()
    use session = Publish.start [] ignored
    Assert.That(session.ToStdout)
    Assert.That(true, "Actual functionality covered by the ConsoleTests")

[<Test>]
let ``HTTP sinks automatically dedupe``() =
    use server = getServer()
    use ignored = new Subject<_>()
    use session = Publish.start [server.Uri; server.Uri] ignored
    Assert.AreEqual(1, List.length session.HttpSinks)

[<Test>]
let ``Publish.stop disconnects HTTP sinks``() =
    use server = getServer()
    use ignored = new Subject<_>()
    use session = Publish.start [server.Uri] ignored
    Publish.stop session
    let e = Assert.Throws<AggregateException>(fun () -> async { do! (List.head session.HttpSinks).Invoke("event", "") |> awaitTask } |> Async.RunSynchronously)
    Assert.IsInstanceOf<InvalidOperationException>(e.InnerException)

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
module PublishTests

open AsyncExtensions
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

//[<Test>]
//let ``http publisher publishes``() =
//    use server = getServer()
//    let spotted = new TaskCompletionSource<bool>(false)
//    use events = Observable.subscribe (fun _ -> spotted.TrySetResult(true) |> ignore) SignalRServer.observedEvents
//    use monitoring = Monitor.start (uniqueName()) [Ping.PingEventSource.GetName(typeof<Ping.PingEventSource>)]
//    use registeredHttpSink = Publish.start [server.Uri] monitoring.Subject
//    async {
//        Ping.ping.Ping()
//        do! spotted.Task |> awaitTask
//    }
//    |> Async.CancelAfterWithCleanup 2000 (fun () -> spotted.TrySetResult(false) |> ignore)
//    |> Async.RunSynchronously
//    |> ignore
//    Assert.That(spotted.Task.IsCompleted)
//    Assert.That(spotted.Task.Result)

[<Test>]
let ``Publish.start [] logs to stdout``() =
    Assert.That(true, "Covered by the ConsoleTests")

//[<Test>]
//let ``HTTP sinks automatically dedupe``() =
//    use server = getServer()
//    use ignored = new Subject<_>()
//    use session = Publish.start [server.Uri; server.Uri] ignored
//    Assert.AreEqual(1, List.length session.HttpSinks)

//[<Test>]
//let ``Publish.stop disconnects HTTP sinks``() =
//    Assert.Fail()

//open Microsoft.AspNet.SignalR.Client
//[<Test>]
//let ``SignalR server works``() =
//    use server = getServer()
//    let spotted = new TaskCompletionSource<bool>(false)
//    use events = Observable.subscribe (fun _ -> spotted.TrySetResult(true) |> ignore) SignalRServer.observedEvents
//    use connection = new HubConnection(server.Uri)
//    let hub = connection.CreateHubProxy("event")
//    async {
//        do! connection.Start() |> awaitTask
//        do! hub.Invoke("event", "ping") |> awaitTask
//        do! spotted.Task |> awaitTask
//    }
//    |> Async.CancelAfterWithCleanup 5000  (fun () -> spotted.TrySetResult(false) |> ignore)
//    |> Async.RunSynchronously
//    |> ignore
//    Assert.That(spotted.Task.Result)
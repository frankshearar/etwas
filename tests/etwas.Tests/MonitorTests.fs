module MonitorTests

open AsyncExtensions
open Microsoft.Diagnostics.Tracing
open Microsoft.Owin.Hosting
open NUnit.Framework
open System
open System.Reactive.Subjects
open System.Threading.Tasks

let uniqueName(): string =
    System.Guid.NewGuid.ToString()

type LoggingDisposable(inner: IDisposable) =
    interface IDisposable with
        override __.Dispose() =
            let typ = inner.GetType().Name
            printfn "Disposing a %s" typ
            inner.Dispose()
            printfn "Disposed a %s" typ

let log x = new LoggingDisposable(x)

[<Test>]
let ``can monitor multiple event sources``() =
    let pingName = Ping.PingEventSource.GetName(typeof<Ping.PingEventSource>)
    let pongName = Ping.PingEventSource.GetName(typeof<Pong.PongEventSource>)
    let spottedPing = new TaskCompletionSource<bool>(false)
    let spottedPong = new TaskCompletionSource<bool>(false)
    use monitoring = Monitor.start (uniqueName()) [pingName; pongName]
    use events = monitoring.Subject
                    |> Observable.subscribe (fun (e: TraceEvent) ->
                                                if e.EventName = pingName then
                                                    spottedPing.TrySetResult(true) |> ignore
                                                else
                                                    spottedPong.TrySetResult(true) |> ignore)
                 |> log
    Ping.ping.Ping()
    Pong.pong.Pong()
    async {
        do! Task.WhenAll [spottedPing.Task; spottedPong.Task] |> awaitTask
    }
    |> Async.CancelAfterWithCleanup 5000 (fun () ->
                                                // Setting the sources means the WhenAll will complete.
                                                spottedPing.TrySetResult(false) |> ignore
                                                spottedPong.TrySetResult(false) |> ignore)
    |> Async.RunSynchronously
    |> ignore
    Assert.That(spottedPing.Task.IsCompleted)
    Assert.That(spottedPong.Task.IsCompleted)
    Assert.That(spottedPing.Task.Result)
    Assert.That(spottedPong.Task.Result)

// Start and stop as one test because the test is less brittle (!) this way: creating
// sessions rapidly that consume the same providers tends to throw Interop.COMExceptions
// about not being able to create files (!) that already exist.
[<Test>]
let ``can start and stop monitoring``() =
    let pingName = Ping.PingEventSource.GetName(typeof<Ping.PingEventSource>)
    let spotted = ref false
    let monitoring = Monitor.start (uniqueName()) [pingName]
    use logger = log monitoring
    Assert.False(monitoring.Clr)
    use events = Observable.subscribe (fun _ ->
                                           spotted := true) monitoring.Subject
    Ping.ping.Ping()
    System.Threading.Thread.Sleep(3000)
    Assert.That(!spotted)

    spotted := false
    Monitor.stop monitoring |> ignore
    Ping.ping.Ping()
    System.Threading.Thread.Sleep(3000)
    Assert.False(!spotted)

[<Test>]
let ``can monitor CLR events``() =
    let spotted = ref false
    let monitoring = Monitor.start (uniqueName()) ["clr"]
    use logger = log monitoring
    Assert.That(monitoring.Clr)
    use events = Observable.subscribe (fun _ ->
                                           spotted := true) monitoring.Subject
    System.Threading.Thread.Sleep(3000)
    Assert.That(!spotted)
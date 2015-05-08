module PublishTests

open Microsoft.Owin.Hosting
open NUnit.Framework
open Microsoft.AspNet.SignalR.Client
open System
open System.Threading.Tasks

let awaitTask(task: Task) =
    async {
        do! task |> Async.AwaitIAsyncResult |> Async.Ignore
        if task.IsFaulted then raise task.Exception
        return ()
    } 

[<Test>]
let ``resolveSink of HTTP URI returns http publisher``() =
    Assert.Fail("Now turn the below into the implementation of http/2")
    use server = WebApp.Start<SignalRServer.Startup>("http://localhost:12345/")
    let connection = new HubConnection("http://localhost:12345/")
    let hub = connection.CreateHubProxy("event")
    async {
        do! awaitTask(connection.Start())
        do! awaitTask(hub.Invoke("event", "ping"))
    } |> Async.RunSynchronously

    let events = !SignalRServer.observedEvents
    Assert.IsNotEmpty(events)
module Listen

open AsyncExtensions
open Microsoft.AspNet.SignalR.Client
open System
open System.Reactive.Subjects

type Conn(url: string) =
    let subject = new Subject<string>()
    let connection = new HubConnection(url)
    let hub = connection.CreateHubProxy("display")
    let events = hub.On("event", fun s -> printfn("recvd"); subject.OnNext(s))
    member __.Subject with get() = subject
    member __.Start() =
        connection.add_ConnectionSlow (fun () -> printfn ">>> connection slow: %s"   url)
        connection.add_Closed         (fun () -> printfn ">>> connection closed: %s" url)
        connection.add_Reconnecting   (fun () -> printfn ">>> reconnectioning: %s"   url)
        connection.add_Reconnected    (fun () -> printfn ">>> reconnectioned: %s"    url)
        connection.add_Error          (fun e  -> printfn ">>> error: %s\n>>> %s"    url (e.ToString()))
        connection.Start()
    interface IDisposable with
        member __.Dispose() =
            events.Dispose()
            subject.Dispose()
//    interface IObservable<string> with
//        member __.Subscribe(obs: IObserver<_>) =
//            subject.Subscribe(obs)

// We return Subject because it implements both IDisposable and IObservable.
let connect (url: string): Async<Conn> =
    async {
        printfn ">>> connecting to %s" url
        let connection = new Conn(url)
        do! awaitTask (connection.Start())
        printfn ">>> connected"
        return connection
    }

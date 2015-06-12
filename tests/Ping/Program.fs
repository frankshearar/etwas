open System
open System.Threading;

let registerExitOnCtrlC (canceller: CancellationTokenSource) =
    Console.CancelKeyPress
    |> Observable.subscribe (fun _ ->
        // Like tears in rain... time to die.
        // We _do not_ close the trace session: call azmon --stop to do that.
        canceller.Cancel())
    |> ignore


[<EntryPoint>]
let main argv =
    let canceller = new CancellationTokenSource()

    let runUntilCancelled = async {
        registerExitOnCtrlC canceller

        while true do
            do! Async.Sleep(1000)
            Ping.ping.Ping()
            printfn "Ping!"
    }
    Async.RunSynchronously(runUntilCancelled, 10000, canceller.Token)
    0

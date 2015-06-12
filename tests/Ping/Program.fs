open Nessos.UnionArgParser
open System
open System.Threading
open System.Reactive.Linq

type Arguments =
    | Interval of int
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Interval _ -> "Time between events. Default: 1s"


let registerExitOnCtrlC (canceller: CancellationTokenSource) =
    Console.CancelKeyPress
    |> Observable.subscribe (fun _ ->
        // Like tears in rain... time to die.
        // We _do not_ close the trace session: call azmon --stop to do that.
        canceller.Cancel())
    |> ignore

let printMessagesPerSecond n =
    printfn "%d msg/s" n

let parser = UnionArgParser.Create<Arguments>()
let usage = parser.Usage()

[<EntryPoint>]
let main argv =
    try
        let args = parser.Parse argv

        let args = parser.Parse (argv, raiseOnUsage = false)
        if args.IsUsageRequested then
            printfn "%s" usage
            0
        else
            let millis = args.GetResult (<@ Interval @>, defaultValue = 1000)

            let canceller = new CancellationTokenSource()

            let msgCount = ref 0L

            let publishMessageRate = async {
                while true do
                    do! Async.Sleep(1000)
                    printMessagesPerSecond (Interlocked.Exchange(msgCount, 0L))
            }

            let publishMessages = async {
                while true do
                    Interlocked.Increment(msgCount) |> ignore
                    if millis > 0 then do! Async.Sleep(millis)
                    Ping.ping.Ping()
            }

            let runUntilCancelled = async {
                registerExitOnCtrlC canceller
                let! _ = Async.Parallel [publishMessages ; publishMessageRate]
                return 0
            }
            Async.RunSynchronously(runUntilCancelled, 10000, canceller.Token)
    with
    | :? System.ArgumentException as e ->
        printfn "%s" usage
        printfn "-------"
        printfn "%A" (e.ToString())
        1
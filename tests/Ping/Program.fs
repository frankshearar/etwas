open Nessos.UnionArgParser
open System
open System.Threading
open System.Reactive.Linq

type Arguments =
    | Rate of int64
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Rate _ -> "(Estimated) events published per second. Default: 1 event/s"

let registerExitOnCtrlC (canceller: CancellationTokenSource) =
    Console.CancelKeyPress
    |> Observable.subscribe (fun _ ->
        // Like tears in rain... time to die.
        canceller.Cancel())
    |> ignore

let printMessagesPerSecond n =
    printfn "%d\tmsg/s" n

let parser = UnionArgParser.Create<Arguments>()
let usage = parser.Usage()

[<EntryPoint>]
let main argv =
    try
        let args = parser.Parse (argv, raiseOnUsage = false)
        if args.IsUsageRequested then
            printfn "%s" usage
            0
        else
            let eventsPerSecond = args.GetResult (<@ Rate @>, defaultValue = 1L)

            let canceller = new CancellationTokenSource()
            let msgCount = ref 0L
            let timer = new System.Diagnostics.Stopwatch()
            let freq = System.Diagnostics.Stopwatch.Frequency
            let ticksPerSend = freq / eventsPerSecond
            printfn "Timer frequency:        %d Hz" System.Diagnostics.Stopwatch.Frequency
            printfn "Timer high resolution?: %A" System.Diagnostics.Stopwatch.IsHighResolution

            let publishMessageRate = async {
                while true do
                    do! Async.Sleep(1000)
                    printMessagesPerSecond (Interlocked.Exchange(msgCount, 0L))
            }

            let publishMessages = async {
                timer.Start()
                while true do
                    if eventsPerSecond > 0L then
                        while timer.ElapsedTicks < ticksPerSend do
                            do! Async.Sleep 0
                    Interlocked.Increment(msgCount) |> ignore
                    Ping.ping.Ping()
                    timer.Restart()
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
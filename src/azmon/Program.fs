module azmon

open Nessos.UnionArgParser
open System
open System.Threading

type Arguments =
    | Source of string
    | Sink of string
    | Stop
    | Debug
    | Install_Counters
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Source _ -> "Publish events from a named ETW event source. Allowed: event provider names, 'clr'. May occur multiple times."
            | Sink _   -> "HTTP URLs, or 'role:InstanceName' for Azure roles, or 'stdout'. No sources means logging to stdout. May occur multiple times."
            | Stop     -> "Stop listening to events (affects ALL running azmon processes). If present, other parameters are ignored."
            | Debug    -> "Print debug information"
            | Install_Counters -> "Install performance counters"

let parser = UnionArgParser.Create<Arguments>()
let usage = parser.Usage()

let registerExitOnCtrlC (canceller: CancellationTokenSource) session =
    Console.CancelKeyPress
    |> Observable.subscribe (fun _ ->
        // Like tears in rain... time to die.
        // We _do not_ close the trace session: call azmon --stop to do that.
        Monitor.stop session |> ignore
        canceller.Cancel())
    |> ignore

let printMessagesPerSecond n =
    eprintfn "%d\tmsg/s" n

[<EntryPoint>]
let main argv =
    try
        if Array.isEmpty argv then
            printfn "%s" usage
            0
        else
            let args = parser.Parse argv

            if args.Contains <@ Install_Counters @> then
                Counter.installCounters()
                0
            else if args.Contains <@ Stop @> then
                Monitor.stopSessionByName "Azmon-Trace-Session" |> ignore
                0
            else
                let receiveCounter = Counter.createCounter "ETW Receive messages per second"
                let messageCounter = Counter.createCounter "Message count"

                let canceller = new CancellationTokenSource()
                let msgCount = ref 0L

                let monitoring = Monitor.start "Azmon-Trace-Session" (args.GetResults <@ Source @>)

                use countEvents = monitoring.Subject
                                  |> Observable.subscribe (fun _ ->
                                    receiveCounter |> Counter.fireCounter
                                    messageCounter |> Counter.fireCounter
                                    Interlocked.Increment(msgCount) |> ignore)

                let publishMessageRate = async {
                    while true do
                        do! Async.Sleep(1000)
                        printMessagesPerSecond (Interlocked.Exchange(msgCount, 0L))
                }

                let monitor = async {
                    let printDebug = args.Contains <@ Debug @>
                    use publishing = monitoring.Subject
                                     |> Publish.start (args.GetResults <@ Sink @>) Counter.createCounter printDebug

                    while true do
                        do! Async.Sleep(1000)
                }

                let runUntilCancelled = async {
                    registerExitOnCtrlC canceller monitoring
                    let! _ = Async.Parallel [monitor ; publishMessageRate]
                    return 0
                }
                Async.RunSynchronously(runUntilCancelled, 10000, canceller.Token)
    with
    | :? System.ArgumentException as e ->
        printfn "%s" usage
        printfn "-------"
        printfn "%A" (e.ToString())
        1
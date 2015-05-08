module azmon

open Nessos.UnionArgParser
open System
open System.Threading

type Arguments =
    | [<Mandatory>] [<EqualsAssignment>] Source of string
    | [<EqualsAssignment>] Sink of string
    | Stop
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
                | Source _ -> "Publish events from a named event source"
                | Sink _ -> "Only support HTTP URLs at the moment. No sources means logging to stdout"
                | Stop -> "Stop listening to events (affects ALL running azmon processes)"

let parser = UnionArgParser.Create<Arguments>()
let usage = parser.Usage()

[<EntryPoint>]
let main argv =
    try
        if Array.isEmpty argv then
            printfn "%s" usage
            0
        else
            let args = parser.Parse argv

            let canceller = new CancellationTokenSource()
            Console.CancelKeyPress
            |> Observable.subscribe (fun _ ->
                // Like tears in rain... time to die.
                // Because it's a clean shutdown, we close the trace
                // session.
                Monitor.stop() |> ignore
                canceller.Cancel()) |> ignore

            let runUntilCancelled = async {
                            Monitor.start    (args.GetResults <@ Source @>)
                            |> Publish.start (args.GetResults <@ Sink @>)

                            while true do
                                do! Async.Sleep(1000)

                            // Do not put any code here. It's on the far
                            // side of an infinite loop, so you'll be
                            // waiting a while for it to execute!

                            return 0
                       }
            Async.RunSynchronously(runUntilCancelled, 10000, canceller.Token)
    with
    | :? System.ArgumentException as e ->
        printfn "%s" usage
        printfn "-------"
        printfn "%A" (e.ToString())
        1
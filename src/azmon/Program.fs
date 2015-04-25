module azmon

open Monitor
open Publish
open Nessos.UnionArgParser
open System
open System.Threading

type Arguments =
    | [<EqualsAssignment>] Source of string
    | [<EqualsAssignment>] Sink of string
    | Stop
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
                | Source _ -> "Publish events from a named event source"
                | Sink _ -> "Only support HTTP URLs at the moment"
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
            |> Observable.subscribe (fun _ -> canceller.Cancel()) |> ignore

            let runUntilCancelled = async {
                            Monitor.start    (args.GetResults <@ Source @>)
                            |> Publish.start (args.GetResults <@ Sink @>)

                            while true do
                                do! Async.Sleep(1000)
                            return 0
                       }
            Async.RunSynchronously(runUntilCancelled, 10000, canceller.Token)
    with
    | :? System.ArgumentException as e ->
        printfn "%s" usage
        printfn "-------"
        printfn "%A" (e.ToString())
        1
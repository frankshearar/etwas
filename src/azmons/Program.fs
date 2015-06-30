module azmons

open Microsoft.Owin.Hosting
open Nessos.UnionArgParser
open System
open System.Threading

type Arguments =
    | Port of int
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Port _ -> "Listen on a particular port. Defaults to 8080"

let parser = UnionArgParser.Create<Arguments>()
let usage = parser.Usage()

let registerExitOnCtrlC (canceller: CancellationTokenSource) =
    Console.CancelKeyPress
    |> Observable.subscribe (fun _ ->
        // Like tears in rain... time to die.
        canceller.Cancel())
    |> ignore

[<EntryPoint>]
let main argv =
    try
        // TODO: Find port number by checking (in order)
        // * command line argument
        // * PORT environment variable (Because of the below, move this as first choice?)
        // * App.config.
        // * default to 8080
        // UnionArgParsre will do command line then App.config. Is that good enough? Heroku-like environments
        // (IIRC at least) want ENV["PORT"]
        let args = parser.Parse (argv, raiseOnUsage = false)
        if args.IsUsageRequested then
            printfn "%s" usage
            0
        else
            let port = args.GetResult (<@ Port @>, defaultValue = 8080)
            let uri = sprintf "http://localhost:%d/" port // Despite the FQDN, this will listen on all interfaces.

            let canceller = new CancellationTokenSource()
            let runUntilCancelled = async {
                    let server = WebApp.Start<Bootstrapper.Startup>(uri)
                    while true do
                        do! Async.Sleep 1000

                    return 0
                }
            Async.RunSynchronously(runUntilCancelled, 10000, canceller.Token)
    with
    | :? System.ArgumentException as e ->
        printfn "%s" usage
        printfn "-------"
        printfn "%A" (e.ToString())
        1
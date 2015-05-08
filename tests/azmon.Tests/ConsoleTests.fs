﻿module ConsoleTests

open NUnit.Framework
open System.Diagnostics
open System.IO
open System.Diagnostics.Tracing

// http://www.fssnip.net/hy =======================
// inspired by http://stackoverflow.com/a/11191070

open System

type private Completed<'T>(value : 'T) =
    inherit Exception()
    member __.Value = value

exception private Timeout

type Async with
    static member CancelAfter timeout (f : Async<'T>) =
        let econt e = Async.FromContinuations(fun (_,econt,_) -> econt e)
        let worker = async {
            let! r = f
            return! econt <| Completed(r)
        }
        let timer = async {
            do! Async.Sleep timeout
            return! econt Timeout
        }

        async {
            try
                let! _ = Async.Parallel [worker ; timer]
                return failwith "unreachable exception reached."
            with
            | :? Completed<'T> as t -> return Some t.Value
            | Timeout -> return None
        }
// =====================================

[<EventSource(Name = "Ping")>]
type PingEventSource() =
    inherit EventSource()
    [<Event(2000)>]
    member __.Ping() =
        __.WriteEvent(2000)

// An EventSource _really really_ wants to be a Singleton.
let ping = new PingEventSource()

let run program args: Process option =
    let start = new ProcessStartInfo()
    start.UseShellExecute <- false
    start.FileName <- program
    start.Arguments <- args
    start.RedirectStandardInput <- true
    start.RedirectStandardError <- true
    start.RedirectStandardOutput <- true
    let p = new Process()
    p.StartInfo <- start
    try
        if p.Start() then
            Some p
        else
            None
    with
    | :? System.ComponentModel.Win32Exception ->
        None

// Run the command, and return the (exit code, stdout)
let sh program args: int * string =
    match run program args with
    | Some p ->
        p.WaitForExit()
        p.ExitCode, p.StandardOutput.ReadToEnd() + "\n-----\n" + p.StandardError.ReadToEnd()
    | None ->
        -1, sprintf "FAILED TO START THE PROCESS %A %A" program args

let azmon = 
    let cwd = new DirectoryInfo(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory))
    let solnDir = cwd.Parent.Parent.Parent.Parent.FullName
    Path.Combine(solnDir, @"src\azmon\bin\Debug\azmon.exe")

[<Test>]
let ``Run with no args shows usage`` () =
    let exitCode, stdout = sh azmon ""
    Assert.That(stdout.Contains("--source"), "No usage displayed in:\n" + stdout)
    Assert.AreEqual(0, exitCode)

[<Test>]
let ``Closes gracefully on receipt of a ^C``() =
    let processForaBit = async {
                            let proc = run azmon (sprintf "--source=%s" ping.Name)
                            try
                                match proc with
                                | Some p ->
                                    p.StandardInput.WriteLine("\x3")
                                    return 0
                                | None -> return 1
                            finally
                                match proc with
                                | Some p -> p.Kill()
                                | None -> ()
                         }
    let result = processForaBit |> Async.CancelAfter 2000 |> Async.RunSynchronously
    match result with
    | Some _ -> ()
    | None -> Assert.Fail("Process didn't quit")

[<Test>]
let ``Can log events out-of-process``() =
    // This will ensure we have at least one interesting event.
    ping.Ping()
    let processForaBit = async {
                            let proc = run azmon (sprintf "--source=%s" ping.Name)
                            try
                                match proc with
                                | Some p ->
                                    do! Async.Sleep 5000
                                    p.StandardInput.WriteLine("\x3")
                                    // I don't know why, but p.StandardOutput.ReadToEnd() blocks forever.
                                    // We ^C'd to stdin - that should make the process stop, surely?!
                                    let lines = [1..5]
                                                |> List.map (fun _ -> p.StandardOutput.ReadLine())
                                                |> List.fold (fun acc each -> acc + each) ""
                                    return lines
                                | None -> return "Process didn't run?"
                            finally
                                match proc with
                                | Some p -> p.Kill()
                                | None -> ()
                         }
    let stdout = processForaBit |> Async.CancelAfter 6000 |> Async.RunSynchronously
    match stdout with
    | Some s ->
        Assert.IsNotEmpty(s)
        // Not an actual event, this shows we see the advertised metadata of the event source.
        Assert.That(s.Contains(@"ProviderName=""Ping"""), (sprintf "Ping event source not found in stdout: %s" s))
    | None -> Assert.Fail("Something went wrong")
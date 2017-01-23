﻿module ConsoleTests

open AsyncExtensions
open NUnit.Framework
open PortUtilities
open System
open System.Diagnostics
open System.IO
open System.Net
open System.Text

type Either<'a> =
    | Right of 'a
    | Left of string

let run program args: Either<Process> =
    let start = new ProcessStartInfo()
    start.CreateNoWindow <- true
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
            printfn "Running! %d" p.Id
            Right p
        else
            Left "Didn't start"
    with
    | :? System.ComponentModel.Win32Exception as e -> // For instance, the file's missing.
        Left (e.ToString())

// Run the command, and return the (exit code, stdout)
let sh program args: int * string =
    match run program args with
    | Right p ->
        p.WaitForExit()
        p.ExitCode, p.StandardOutput.ReadToEnd() + "\n-----\n" + p.StandardError.ReadToEnd()
    | Left e ->
        -1, sprintf "FAILED TO START THE PROCESS %A %A (%s)" program args e

let etwas =
    let cwd = new DirectoryInfo(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory))
    let solnDir = cwd.Parent.Parent.Parent.Parent.FullName
    Path.Combine(solnDir, @"src\etwas\bin\Debug\etwas.exe")

let etwass =
    let cwd = new DirectoryInfo(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory))
    let solnDir = cwd.Parent.Parent.Parent.Parent.FullName
    Path.Combine(solnDir, @"src\etwass\bin\Debug\etwass.exe")

[<TestFixture>]
type Etwas() =
    [<TearDown>]
    member x.``Kill OS trace session``() =
        sh etwas "--stop" |> ignore

    [<Test>]
    member x.``shows usage when run with no args`` () =
        let exitCode, stdout = sh etwas ""
        Assert.That(stdout.Contains("--source"), "No usage displayed in:\n" + stdout)
        Assert.AreEqual(0, exitCode)

    [<Test>]
    member x.``closes gracefully on receipt of a ^C``() =
        let processForaBit = async {
                                let proc = run etwas (sprintf "--source %s" Ping.ping.Name)
                                try
                                    match proc with
                                    | Right p ->
                                        p.StandardInput.WriteLine("\x3")
                                        return 0
                                    | Left _ -> return 1
                                finally
                                    match proc with
                                    | Right p -> p.Kill()
                                    | Left _ -> ()
                             }
        let result = processForaBit |> Async.CancelAfter 2000 |> Async.RunSynchronously
        match result with
        | Some _ -> ()
        | None -> Assert.Fail("Process didn't quit")

    [<Test>]
     member x.``supports HTTP sinks``() =
         let proc = run etwass "--source ping --sink http://localhost:8080/"
         match proc with
         | Right p ->
             let s = p.StandardOutput.ReadToEnd()
             Assert.False(s.Contains "Unhandled Exception")
         | Left e -> Assert.Fail(sprintf "Failed to start etwass: %s" (e.ToString()))

    [<Test>]
    member x.``can log events out-of-process``() =
        // This will ensure we have at least one interesting event.
        Ping.ping.Ping()
        let processForaBit = async {
                                let proc = run etwas (sprintf "--source %s" Ping.ping.Name)
                                match proc with
                                | Right p ->
                                    let output = new StringBuilder()
                                    let error = new StringBuilder()
                                    p.OutputDataReceived.Add(fun args -> output.Append(args.Data) |> ignore)
                                    p.ErrorDataReceived.Add(fun args -> error.Append(args.Data) |> ignore)
                                    p.BeginErrorReadLine()
                                    p.BeginOutputReadLine()
                                    do! Async.Sleep 5000 // Fairly arbitrary pause; seems like out-of-process ETW logging has a latency of ~2 seconds.
                                    p.Kill()
                                    return output.Append(error.ToString()).ToString()
                                | Left e -> return sprintf "Process didn't run? (%s)" e
                             }
        let stdout = processForaBit |> Async.CancelAfter 6000 |> Async.RunSynchronously
        match stdout with
        | Some s ->
            Assert.IsNotEmpty(s)
            // Not an actual event, this shows we see the advertised metadata of the event source.
            Assert.That(s.Contains(@"ProviderName=""Ping"""), (sprintf "Ping event source not found in stdout: %s" s))
        | None -> Assert.Fail("Something went wrong")
    [<Test>]
    member x.``stops session gracefully``() =
        // Actually this test just shows that we don't barf when asked to --stop.
        let exitCode, _ = sh etwas "--stop"
        Assert.AreEqual(0, exitCode)

[<TestFixture>]
type Etwass() =
    [<Test>]
    member x.``closes gracefully on receipt of a ^C``() =
        let processForaBit = async {
                                let proc = run etwass "--port 8080" // TODO: Remove hard-coded port
                                try
                                    match proc with
                                    | Right p ->
                                        p.StandardInput.WriteLine("\x3")
                                        return 0
                                    | Left _ -> return 1
                                finally
                                    match proc with
                                    | Right p -> p.Kill()
                                    | Left _ -> ()
                                }
        let result = processForaBit |> Async.CancelAfter 2000 |> Async.RunSynchronously
        match result with
        | Some _ -> ()
        | None -> Assert.Fail("Process didn't quit")

    [<Test>]
    member x.``starts listening on specified port``() =
        let proc = run etwass "--port 8081"
        try
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds 1.0) // It takes as long as this to actually spin up the process and register the socket!
            match run "netstat" "-anp TCP" with
            | Right p ->
                let output = p.StandardOutput.ReadToEnd()
                let ports = output.Split([|"\r\n"|], StringSplitOptions.RemoveEmptyEntries)
                            |> Array.filter (fun s -> s.Contains "LISTENING")
                            |> Array.filter (fun s -> s.Contains "8081")
                CollectionAssert.IsNotEmpty(ports, "Nothing listening")
            | Left e -> Assert.Fail(sprintf "Couldn't run netstat: %s" (e.ToString()))
        finally
        match proc with
        | Right p -> p.Kill()
        | Left _ -> ()

    [<Test>]
    member x.``shows usage when asked``() =
        let proc = run etwass "--help"
        match proc with
        | Right p ->
            let s = p.StandardOutput.ReadToEnd()
            Assert.That(s, Contains.Substring("--help"))
            Assert.That(s, Contains.Substring("--port"))
            Assert.AreEqual(0, p.ExitCode)
        | Left e -> Assert.Fail(sprintf "Failed to start etwass: %s" (e.ToString()))

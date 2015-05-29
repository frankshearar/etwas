module ConsoleTests

open AsyncExtensions
open NUnit.Framework
open System
open System.Diagnostics
open System.IO
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

let azmon =
    let cwd = new DirectoryInfo(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory))
    let solnDir = cwd.Parent.Parent.Parent.Parent.FullName
    Path.Combine(solnDir, @"src\azmon\bin\Debug\azmon.exe")

[<TearDown>]
let ``Kill OS trace session``() =
    sh azmon "--stop" |> ignore

[<Test>]
let ``Run with no args shows usage`` () =
    let exitCode, stdout = sh azmon ""
    Assert.That(stdout.Contains("--source"), "No usage displayed in:\n" + stdout)
    Assert.AreEqual(0, exitCode)

[<Test>]
let ``Closes gracefully on receipt of a ^C``() =
    let processForaBit = async {
                            let proc = run azmon (sprintf "--source %s" Ping.ping.Name)
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
let ``Can log events out-of-process``() =
    // This will ensure we have at least one interesting event.
    Ping.ping.Ping()
    let processForaBit = async {
                            let proc = run azmon (sprintf "--source %s" Ping.ping.Name)
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
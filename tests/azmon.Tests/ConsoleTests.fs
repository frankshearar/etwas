module ConsoleTests

open Microsoft.Diagnostics.Tracing.Session
open NUnit.Framework
open System.Diagnostics
open System.IO
open System
open System.Diagnostics.Tracing
open System.Threading

[<EventSource(Name = "Ping")>]
type PingEventSource() =
    inherit EventSource()
    [<Event(2000)>]
    member __.Ping() =
        __.WriteEvent(2000)
    [<Event(2001)>]
    member __.Pong() =
        __.WriteEvent(2001)

// An EventSource _really really_ wants to be a Singleton.
let ping = new PingEventSource()

// Run the command, and return the (exit code, stdout)
let sh program args: int * string =
    let start = new ProcessStartInfo()
    start.UseShellExecute <- false
    start.FileName <- program
    start.Arguments <- args
    start.RedirectStandardError <- true
    start.RedirectStandardOutput <- true
    use p = new Process()
    p.StartInfo <- start
    try
        if p.Start() then
            p.WaitForExit()
            p.ExitCode, p.StandardOutput.ReadToEnd() + "\n-----\n" + p.StandardError.ReadToEnd()
        else
            -1, sprintf "FAILED TO START THE PROCESS %A %A" program args
    with
    | :? System.ComponentModel.Win32Exception as e ->
        -1, sprintf "%s: %s running %A %A" (e.GetType().Name) e.Message program args

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
let ``Run, printing to console, will print events``() =
    // This will ensure we have at least one interesting event in the session
    let sess = new TraceEventSession("Azmon-Trace-Session", null)
    ping.Ping()
    let canceller = new CancellationTokenSource()
    canceller.CancelAfter(5000);
    let processForaBit = async {
                            return sh azmon (sprintf "--source=%s" ping.Name)
                         }
    let _, stdout = Async.RunSynchronously(processForaBit, cancellationToken = canceller.Token)
    // Don't check the error code, because we exit abnormally.
    Assert.IsNotEmpty(stdout)
    Assert.That(stdout.Contains("ping"), (sprintf "ping not found in stdout: %s" stdout))
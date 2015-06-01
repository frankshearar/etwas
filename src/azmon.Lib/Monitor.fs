module Monitor

open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Session
open System.Reactive.Subjects

// Problems:
//* Tests are SUPER flaky
//* Need to separate session creation from session listening: privilege separation

type Session = {
                Name: string
                Trace: TraceEventSession
                Source: ETWTraceEventSource
                Subject: Subject<TraceEvent>
                Clr: bool
               }
    with
    interface System.IDisposable with
        member x.Dispose() =
                x.Subject.OnCompleted() // Tell our listeners we're done!
                x.Subject.Dispose()
                // Don't Dispose() Trace, because that should live on until we call Monitor.stop.

let (|Null|Value|) (x: System.Nullable<_>) =
    // Apparently GetValueOrDefault is faster, and the Unchecked.defaultof<> cannot
    // be reached.
    if x.HasValue then Value (x.GetValueOrDefault(Unchecked.defaultof<_>)) else Null

// Enabling providers requires elevated permissions
let private enableProviders (names: string list) (session: Session) =
//    match TraceEventSession.IsElevated() with
//    | Value true -> enableProviders names sess
//    | Value false
//    | Null -> ()
    names
    |> List.iter (fun s -> printfn "Enabling %s" s
                           session.Trace.EnableProvider s |> ignore
                           printfn "Enabled %s" s)

let private startSession (source: ETWTraceEventSource) =
    async {
        do source.Process() |> ignore
    } |> Async.Start

// Stop monitoring. This disposes the event session in the OS, so will stop receiving
// events for all running azmon processes on this machine. Further, you
// will not collect events in the session until you call start again.
let stopSessionByName name =
    let sess = new TraceEventSession(name, null)
    sess.Stop()

let createSession name withClr =
    let subject = new Subject<_>()
    let sess = new TraceEventSession(name, null)
    // Persist the session past process death. This means
    // a) if you quickly restart azmon, you lose no data;
    // b) the kernel will buffer up to 64MB.
    sess.StopOnDispose <- false
    let source = new ETWTraceEventSource(name, TraceEventSourceType.Session)
    source.Dynamic.add_All (fun evt -> subject.OnNext evt)
    if withClr then
        source.Clr.add_All (fun evt -> subject.OnNext evt)
    {Name = name; Clr = withClr; Trace = sess; Source = source; Subject = subject}

// The entry point to the monitoring.
//
// We create a well-known TraceEventSession, enable all the declared
// providers (event source names), and every time we pick up a message, we
// push it to the returned IObservable.
//
// This ETW session is shared across all running azmon processes. The Session
// instance returned will stop sending events when it is Dispose()d.
let start name (names: string list) =
    let clr = List.exists (fun x -> x = "clr") names
    let srcs = List.filter (fun x -> x <> "clr") names
    let sess = createSession name clr
    enableProviders srcs sess
    startSession sess.Source
    sess

// Stop monitoring. This disposes the event session, so will stop receiving
// events for all running azmon processes on this machine. Further, you
// will not collect events in the session until you call start again.
let stop (session: Session) =
    let r = session.Trace.Stop(noThrow = true)
    session.Trace.Dispose()
    r
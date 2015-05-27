module Monitor

open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Session
open System.Reactive.Subjects

// Do we need unique sessions for azmon processes? (difficult to clean up)
// Should we use unique sessions for sets of sources? So should azmon processes
// listening to Foo and Bar events share the same session? (difficult to clean up)
// Keeping the session alive's useful for protecting against azmon crashes
// Do TraceEventSessions survive reboots? (Probably not: other kernel objects like
// monitors & sockets don't)

// Problems:
//* Tests are SUPER flaky
//* Need to separate session creation from session listening: privilege separation

type Session = {
                Name: string
                Trace: TraceEventSession
                Source: ETWTraceEventSource
                Subject: Subject<TraceEvent>
               }
    with
    interface System.IDisposable with
        member x.Dispose() =
            x.Subject.Dispose()

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

let createSession name =
    let subject = new Subject<_>()
    let sess = new TraceEventSession(name, null)
    // Persist the session past process death. This means
    // a) if you quickly restart azmon, you lose no data;
    // b) the kernel will buffer up to 64MB.
    sess.StopOnDispose <- false
    let source = new ETWTraceEventSource(name, TraceEventSourceType.Session)
    let parser = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventParser(source)
    parser.add_All (fun evt -> subject.OnNext evt)
    {Name = name; Trace = sess; Source = source; Subject = subject}

// The entry point to the monitoring.
//
// We create a well-known TraceEventSession, enable all the declared
// providers (event source names), and every time we pick up a message, we
// push it to the returned IObservable.
//
// This session is shared across all running azmon processes.
let start name (names: string list) =
    let sess = createSession name
    enableProviders names sess
    startSession sess.Source
    sess

// Stop monitoring. This disposes the event session, so will stop receiving
// events for all running azmon processes on this machine. Further, you
// will not collect events in the session until you call start again.
let stop session =
    let r = session.Trace.Stop(noThrow = true)
    session.Trace.Dispose()
    r
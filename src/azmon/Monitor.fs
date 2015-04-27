module Monitor

open Microsoft.Practices.EnterpriseLibrary.SemanticLogging
open Microsoft.Diagnostics.Tracing
open Microsoft.Diagnostics.Tracing.Session
open System.Diagnostics.Tracing
open System
open System.Reactive.Subjects

// Do we need unique sessions for azmon processes? (difficult to clean up)
// Should we use unique sessions for sets of sources? So should azmon processes
// listening to Foo and Bar events share the same session? (difficult to clean up)
// Keeping the session alive's useful for protecting against azmon crashes
// Do TraceEventSessions survive reboots?

let enableProviders (names: string list) (session: TraceEventSession) =
    names
    |> List.map (fun s -> session.EnableProvider s)
    |> ignore // Map, not iter, because it looks slightly less rubbish to ignore at the end

// The entry point to the monitoring.
//
// We create a well-known TraceEventSession, enable all the declared
// providers, and every time we pick up a message, we push it to
// the returned IObservable.
//
// This session is shared across all running azmon processes.
let start (names: string list) =
    let subject = new Subject<_>()
    let sess = new TraceEventSession("Azmon-Trace-Session", null)
    sess.StopOnDispose <- false
    let source = new ETWTraceEventSource("Azmon-Trace-Session", TraceEventSourceType.Session)
    let parser = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventParser(source)
    parser.add_All (fun evt -> subject.OnNext evt)
    enableProviders names sess
    async {
        do source.Process() |> ignore
    } |> Async.Start
    subject

// Stop monitoring. This disposes the event session, so will stop receiving
// events for all running azmon processes on this machine. Further, you
// will not collect events in the session until you call start again.
let stop =
    let sess = new TraceEventSession("Azmon-Trace-Session", null)
    sess.Stop()
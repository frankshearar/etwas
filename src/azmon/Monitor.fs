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

type Session<'a> = {
                Trace: TraceEventSession
                Subject: Subject<'a>
               }

let currentSession: Session<_> option ref = ref None

let enableProviders (names: string list) (session: TraceEventSession) =
    names
    |> List.map (fun s -> session.EnableProvider s)
    |> ignore // Map, not iter, because it looks slightly less rubbish to ignore at the end

let createSession (names: string list) =
    let subject = new Subject<_>()
    let sess = new TraceEventSession("Azmon-Trace-Session", null)
    // Persist the session past process death. This means
    // a) if you quickly restart azmon, you lose no data;
    // b) the kernel will buffer up to 64MB.
    sess.StopOnDispose <- false
    let source = new ETWTraceEventSource("Azmon-Trace-Session", TraceEventSourceType.Session)
    let parser = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventParser(source)
    parser.add_All (fun evt -> subject.OnNext evt)
    enableProviders names sess
    async {
        do source.Process() |> ignore
    } |> Async.Start
    sess, subject

// The entry point to the monitoring.
//
// We create a well-known TraceEventSession, enable all the declared
// providers (event source names), and every time we pick up a message, we
// push it to the returned IObservable.
//
// This session is shared across all running azmon processes.
let start (names: string list) =
    match !currentSession with
    | None ->
        let sess, subject = createSession names
        currentSession := Some {Trace = sess; Subject = subject}
        subject
    | Some sess ->
        sess.Subject

// Stop monitoring. This disposes the event session, so will stop receiving
// events for all running azmon processes on this machine. Further, you
// will not collect events in the session until you call start again.
let stop() =
    match !currentSession with
    | Some sess ->
        let r = sess.Trace.Stop(noThrow = true)
        sess.Trace.Dispose()
        r
    | None ->
        true
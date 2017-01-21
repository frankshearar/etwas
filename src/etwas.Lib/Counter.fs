module Counter

open System.Diagnostics

let counterCreationData name ctype = new CounterCreationData(name, "", ctype)
let counters = [|
                counterCreationData "ETW Receive messages/second" PerformanceCounterType.RateOfCountsPerSecond64
                counterCreationData "Server Receive messages/second" PerformanceCounterType.RateOfCountsPerSecond64
                counterCreationData "Message count" PerformanceCounterType.NumberOfItems64
                counterCreationData "Total publishing buffer size" PerformanceCounterType.NumberOfItems64
               |]

let installCounters() =
    if PerformanceCounterCategory.Exists("etwas Stats") then
        PerformanceCounterCategory.Delete("etwas Stats")
    PerformanceCounterCategory.Create("etwas Stats",
                                      "",
                                      PerformanceCounterCategoryType.MultiInstance,
                                      new CounterCreationDataCollection(counters))
    |> ignore

let createCounter name =
    try
        let curProc = Process.GetCurrentProcess()
        Some (new PerformanceCounter("Etwas Stats", name, curProc.Id.ToString(), false))
    with ex ->
        printfn "Warning: failed to initialise counter \"%s\". Error: %s" name (ex.ToString())
        None

let fireCounter (counter: PerformanceCounter option) =
    match counter with
    | Some x -> x.Increment() |> ignore
    | None   -> ()

let setCounter (counter: PerformanceCounter option) newValue =
    match counter with
    | Some x -> x.RawValue <- newValue
    | None   -> ()

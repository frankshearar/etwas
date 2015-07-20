module Counter

open System.Diagnostics

let counterCreationData name ctype = new CounterCreationData(name, "", ctype)
let counters = [|
                counterCreationData "Receive messages per second" PerformanceCounterType.RateOfCountsPerSecond64
                counterCreationData "Publish messages per second" PerformanceCounterType.RateOfCountsPerSecond64
                counterCreationData "Message count" PerformanceCounterType.NumberOfItems64
               |]

let installCounters() =
    if PerformanceCounterCategory.Exists("azmon Stats") then
        PerformanceCounterCategory.Delete("azmon Stats")
    PerformanceCounterCategory.Create("azmon Stats",
                                      "",
                                      PerformanceCounterCategoryType.MultiInstance,
                                      new CounterCreationDataCollection(counters))
    |> ignore

let createCounter name =
    try
        let curProc = Process.GetCurrentProcess()
        Some (new PerformanceCounter("azmon Stats", name, curProc.Id.ToString(), false))
    with ex ->
        printfn "Warning: failed to initialise counter \"%s\". Error: %s" name (ex.ToString())
        None

let fireCounter (counter: PerformanceCounter option) =
    match counter with
    | Some x -> x.Increment() |> ignore
    | None   -> ()

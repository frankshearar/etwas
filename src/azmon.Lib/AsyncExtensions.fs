module AsyncExtensions

// http://www.fssnip.net/hy =======================
// inspired by http://stackoverflow.com/a/11191070

open System

type private Completed<'T>(value : 'T) =
    inherit Exception()
    member __.Value = value

exception private Timeout

type Async with
    static member CancelAfterWithCleanup timeout (action: (_ -> unit)) (f : Async<'T>) =
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
            | Timeout -> action(); return None
        }

    static member CancelAfter timeout (f : Async<'T>) =
        Async.CancelAfterWithCleanup timeout (fun _ -> ()) f

// =====================================

open System.Threading.Tasks
let awaitTask(task: Task) =
    async {
        do! task |> Async.AwaitIAsyncResult |> Async.Ignore
        if task.IsFaulted then raise task.Exception
        return ()
    }

let doItNow(t: Task) =
    async {
        do! awaitTask t
    } |> Async.RunSynchronously
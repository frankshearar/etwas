module azmons

open Nessos.UnionArgParser

type Arguments =
    | [<EqualsAssignment>] Port of int
with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Port _ -> "Listen on a particular port. Defaults to 8080"

let parser = UnionArgParser.Create<Arguments>()
let usage = parser.Usage()

[<EntryPoint>]
let main argv =
    try
        if Array.isEmpty argv then
            printfn "%s" usage
            0
        else
            0
    with
    | :? System.ArgumentException as e ->
        printfn "%s" usage
        printfn "-------"
        printfn "%A" (e.ToString())
        1
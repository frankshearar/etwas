namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("azmon")>]
[<assembly: AssemblyProductAttribute("azmon")>]
[<assembly: AssemblyDescriptionAttribute("Publish ETW events to all the places.")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"

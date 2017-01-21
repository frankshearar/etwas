#r @"..\..\packages\EnterpriseLibrary.SemanticLogging\lib\net45\Microsoft.Practices.EnterpriseLibrary.SemanticLogging.dll"
#r @"..\..\packages\Microsoft.Diagnostics.Tracing.TraceEvent\lib\net40\Microsoft.Diagnostics.Tracing.TraceEvent.dll"
//#r @"..\..\packages\FSharp.Control.Reactive\lib\net40\FSharp.Control.Reactive.dll"
#r @"..\..\packages\Rx-Core\lib\net45\System.Reactive.Core.dll"
#r @"..\..\packages\Rx-Interfaces\lib\net45\System.Reactive.Interfaces.dll"
#r @"..\..\packages\Rx-Linq\lib\net45\System.Reactive.Linq.dll"
open Microsoft.Practices.EnterpriseLibrary.SemanticLogging
open System.Reactive.Linq
open Microsoft.Diagnostics.Tracing

let sess = new Microsoft.Diagnostics.Tracing.Session.TraceEventSession("WTF", null)
let source = new ETWTraceEventSource("WTF", TraceEventSourceType.Session)
let parser = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventParser(source)
parser.add_All (fun evt -> printfn "%s" (evt.ToString()))
sess.EnableProvider("Thing")
sess.EnableProvider("Ping")
sess.EnableProvider("Notathing")
source.Process()
3


sess.Dispose()

3
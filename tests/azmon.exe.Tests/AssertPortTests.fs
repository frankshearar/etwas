module AssertPortTests

open NUnit.Framework
open PortUtilities
open System
open System.Net
open System.Net.Sockets

let AssertPortFreeHasDecentErrorMessage (portState: string) (e: Exception) (port: IPEndPoint) =
    Assert.That(e.Message, Contains.Substring(portState), "No mention of error")
    Assert.That(e.Message, Contains.Substring(port.Address.ToString()), "No mention of address")

let loopbacks = [|
                    new IPEndPoint(IPAddress.Loopback, 12341)
                    new IPEndPoint(IPAddress.IPv6Loopback, 12341)
                 |]

[<TestCaseSource("loopbacks")>]
let ``First free TCP port starts from base``(start: IPEndPoint) =
    AssertPort.Free start
    let found = AssertPort.FindFirstFreeTcpPort start
    Assert.AreEqual(start.Port, found.Port)

[<TestCaseSource("loopbacks")>]
let ``First free TCP port skips busy ports``(start: IPEndPoint) =
    AssertPort.Free start
    let l = new TcpListener(start)
    try
        l.Start()
        let found = AssertPort.FindFirstFreeTcpPort start
        Assert.That(start.Port < found.Port, (sprintf "Found port %d but start port was %d" found.Port start.Port))
    finally
        l.Stop()

[<Test>]
let ``Free error message mentions address and port``() =
    let port = AssertPort.FindFirstFreeTcpPort()
    AssertPort.Free(port)
    let l = new TcpListener(port)
    l.Start()
    try
        let e = Assert.Throws<AssertionException>(fun () -> AssertPort.Free(port))
        AssertPortFreeHasDecentErrorMessage "busy" e port
    finally
        l.Stop()

[<TestCaseSource("loopbacks")>]
let ``Free fails on busy port``(port: IPEndPoint) =
    let l = new TcpListener(port)
    try
        l.Start()
        Assert.Throws<AssertionException>(fun () -> AssertPort.Free(port))
        |> ignore
    finally
        l.Stop()

[<TestCaseSource("loopbacks")>]
let ``Free passes on free port``(port: IPEndPoint) =
    // We can't use AssertPort.Free to test that AssertPort.Free works!
    let l = new TcpListener(port)
    try
        l.Start() // If this blows up, the port wasn't free...
    finally
        l.Stop() // So at this point the port _is_ free (probably)
    AssertPort.Free(port)

[<Test>]
let ``Used error message mentions address and port``() =
    let port = AssertPort.FindFirstFreeTcpPort()
    AssertPort.Free port
    let e = Assert.Throws<AssertionException>(fun () -> AssertPort.Used port)
    AssertPortFreeHasDecentErrorMessage "free" e port

[<TestCaseSource("loopbacks")>]
let ``Used fails on free port``(port: IPEndPoint) =
    let l = new TcpListener(port)
    try
        l.Start() // If this blows up, the port wasn't free...
    finally
        l.Stop() // So at this point the port _is_ free (probably)
    Assert.Throws<AssertionException>(fun () -> AssertPort.Used port)
    |> ignore


[<TestCaseSource("loopbacks")>]
let ``Used passes on used port``(port: IPEndPoint) =
    let l = new TcpListener(port)
    try
        l.Start()
        Assert.DoesNotThrow(fun () -> AssertPort.Used port)
    finally
        l.Stop()
module PortUtilities

open NUnit.Framework
open System
open System.Net
open System.Net.Sockets

type AssertPort() =
    static member IsFree (socket: IPEndPoint) =
        let WSAEADDRINUSE = 10048
        let s = new TcpListener(socket)
        try
            try
                s.Start()
                true
            with
            | :? SocketException as e ->
                if e.ErrorCode = WSAEADDRINUSE then
                    false
                else
                    raise (new Exception(sprintf "Unexpected error %d binding to %s" e.ErrorCode (socket.ToString())))
        finally
            s.Stop()

    static member Free (socket: IPEndPoint) =
        if not(AssertPort.IsFree socket) then
            Assert.Fail(sprintf "Port %s is busy" (socket.ToString()))

    static member Used (socket: IPEndPoint) =
        let s = new TcpListener(socket)
        try
            try
                s.Start()
                Assert.Fail(sprintf "Port %s is free" (socket.ToString()))
            with
            | :? SocketException -> ()
        finally
            s.Stop()

    static member Addresses() =
        Seq.initInfinite (fun _ -> IPAddress.Loopback)

    static member FindAvailableAddress() =
        AssertPort.Addresses()
        |> Seq.skip (int(DateTime.Now.TimeOfDay.TotalSeconds))
        |> Seq.head

    static member FindFirstFreeTcpPort (?startingPoint: IPEndPoint) =
        let startAddr, startPort = match startingPoint with
                                   | Some endpoint -> endpoint.Address, endpoint.Port
                                   | None          -> AssertPort.FindAvailableAddress(), 10000
        let port i =
            new IPEndPoint(startAddr, startPort + i)
        Seq.init 1000 (fun i -> port i)
        |> Seq.filter (fun current -> AssertPort.IsFree current)
        |> Seq.head
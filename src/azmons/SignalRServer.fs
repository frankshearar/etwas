// A very simple test fixture that simply collects events it sees in a list ref.
module SignalRServer

open Owin
open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open Microsoft.Owin
open Nancy
open System.Reactive.Subjects

let observedEvents = new Subject<string>()

type Startup() =
    member __.Configuration(app: IAppBuilder) = app.MapSignalR().UseNancy() |> ignore

[<assembly: OwinStartup(typeof<Startup>)>]
do ()

type Bootstrapper() =
     inherit DefaultNancyBootstrapper()

[<HubName("event")>]
type EventHub() =
    inherit Hub()
    member __.event(xml) =
        observedEvents.OnNext(xml)
        box ""
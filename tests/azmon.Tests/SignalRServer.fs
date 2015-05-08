module SignalRServer

open Owin
open Microsoft.AspNet.SignalR
open Microsoft.AspNet.SignalR.Hubs
open Microsoft.Owin
open Nancy
open Nancy.Owin

let observedEvents: string list ref = ref []

type Startup() =
    member __.Configuration(app: IAppBuilder) = app.MapSignalR().UseNancy() |> ignore

[<assembly: OwinStartup(typeof<Startup>)>]
do ()

type Bootstrapper() =
     inherit DefaultNancyBootstrapper()

[<HubName("event")>]
type EventHub() =
    inherit Hub()
    member x.event(event: string) =
        observedEvents := event :: (!observedEvents)
        box "pong"
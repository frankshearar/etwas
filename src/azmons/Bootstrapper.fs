module Bootstrapper

open Owin
open Microsoft.AspNet.SignalR
open Microsoft.Owin
open Nancy
open Nancy.Bootstrapper
open Nancy.Conventions
open Nancy.TinyIoc
open FrontEndServer

type Startup() =
    member __.Configuration(app: IAppBuilder) =
        app.MapSignalR().UseNancy() |> ignore
        printfn "Owin configured"

[<assembly: OwinStartup(typeof<Startup>)>]
do ()

type Bootstrapper() =
    inherit DefaultNancyBootstrapper()
    static let mutable pubHub: Publisher = Unchecked.defaultof<Publisher> // Eww. mutable global state.
    override __.ApplicationStartup(container:TinyIoCContainer, pipelines:IPipelines) =
        StaticConfiguration.DisableErrorTraces <- false
        base.ApplicationStartup(container,pipelines)
        pubHub <- new Publisher(SignalRServer.observedEvents, GlobalHost.ConnectionManager.GetHubContext<DisplayHub>().Clients)
        printfn "Bootstrapped Nancy"
    override __.ConfigureConventions(conventions: NancyConventions) =
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("scripts","Scripts",[|"js"|])) |> ignore

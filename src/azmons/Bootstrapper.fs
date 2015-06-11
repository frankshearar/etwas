module Bootstrapper

open Owin
open Microsoft.AspNet.SignalR
open Microsoft.Owin
open Nancy
open Nancy.Bootstrapper
open Nancy.Conventions
open Nancy.TinyIoc
open FrontEndServer
//open System.Reactive.Subjects

type Resolver(container: TinyIoCContainer) =
    interface IDependencyResolver with
        member __.GetService serviceType =
            printfn "Resolving %s" serviceType.Name
            let resolved, resolution = container.TryResolve serviceType
            if resolved then
                resolution
            else
                null
        member __.GetServices serviceType =
            printfn "Resolving (all) %s" serviceType.Name
            container.ResolveAll serviceType
        member __.Register((serviceType: System.Type), (activator: System.Func<obj>)) =
            printfn "Registering a %s" serviceType.Name
            container.Register(serviceType, activator) |> ignore
        member __.Register((serviceType: System.Type), (activators: System.Collections.Generic.IEnumerable<System.Func<obj>>)) =
            printfn "Registering multiple %s" serviceType.Name
            activators
            |> Seq.map (fun act -> container.Register(serviceType, act))
            |> ignore
        member __.Dispose() =
            // We don't own the container, so we sholdn't dispose it.
            ()



type Startup() =
    member __.Configuration(app: IAppBuilder) =
        // config is... nonsense. We have to create it here a) so that Owin can pass
        // it to SignalR but b) so that on application startup Nancy can build a
        // DependencyResolver for SignalR!
//        let config = new HubConfiguration()
//        let signalrThings = new TinyIoCContainer()
//        signalrThings.Register<_,_>(SignalRServer.observedEvents) |> ignore
//        config.Resolver <- new Resolver(signalrThings)
        app.MapSignalR((*config*)).UseNancy() |> ignore
        printfn "Owin configured"

[<assembly: OwinStartup(typeof<Startup>)>]
do ()

type Bootstrapper() =
    inherit DefaultNancyBootstrapper()
    static let mutable pubHub: Publisher = Unchecked.defaultof<Publisher>
    override __.ApplicationStartup(container:TinyIoCContainer, pipelines:IPipelines) =
        StaticConfiguration.DisableErrorTraces <- false
        base.ApplicationStartup(container,pipelines)
        pubHub <- new Publisher(SignalRServer.observedEvents, GlobalHost.ConnectionManager.GetHubContext<DisplayHub>().Clients)
        printfn "Bootstrapped Nancy"
    override __.ConfigureConventions(conventions: NancyConventions) =
        conventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("scripts","Scripts",[|"js"|])) |> ignore

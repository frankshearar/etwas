# Etwas

Etwas provides a clean, configurable way to publish Event Tracing for Windows (ETW) events to remote locations.

Etwas is primarily intended for monitoring machines in an Azure deployment, but should work fine on any Windows machine.

On every machine in your deployment, run `etwas.exe`. On your aggregators (you may have as many as you like) run `etwass.exe` ("s" for "server").

To see the events in your aggregators, either connect to one through your browser or point `etwasc.exe` ("c" for "client") to one. `etwasc` will
dump events to the console.

## How to use it

    $ etwas.exe

            --source <string>: Publish events from a named ETW event source. Allowed: event provider names, 'stdout', 'clr'. May occur multiple times.
            --sink <string>: 'HTTP URLs, or 'role:InstanceName' for Azure roles, or 'stdout'. No sources means logging to stdout. May occur multiple times.
            --stop: Stop listening to events (affects ALL running etwas processes). If present, other parameters are ignored.
            --help [-h|/h|/help|/?]: display this list of options.

    $ etwass.exe

           --port <int>: Listen on a particular port. Defaults to 8080
           --help [-h|/h|/help|/?]: display this list of options.

    $ etwasc

            --server <string>: HTTP/S URI of Etwas server
            --help [-h|/h|/help|/?]: display this list of options.

## TODO

* Keep etwas.exe running when etwass.exe quits. (Or, handle server disconnects gracefully.)
* Revisit the way we register sinks - users don't want to have nuget dependencies on ALL THE TYPES
* Remove hardcoded "event" SignalR hub in the client.
* UI, so you can see events in your browser.
* "Raw" connection for console your own sinks off the aggregated logging.
* Privilege separation, so that setting up the ETW trace session can (must) be done elevated, but the actual monitoring can be unprivileged.
* tracking CPU usage, and other system statuses. (Use a statsd-like format? Lower bandwidth requirements than XML events.)
* tracking _per process_ CPU usage, possibly on an opt-in basis. (Use a statsd-like format? Lower bandwidth requirements than XML events.)
* Make the tests more robust, especially in NCrunch.

## Technical details

Etwas starts an ETW trace session, and whenever it sees events, uses [SignalR](http://signalr.net/) to publish events to the aggregators.

In turn the aggregators use SignalR to publish these chunks of XML to your browser, or any other kind of connected client.

## Testing

Etwas also contains a load/smoke test event producer, Ping.exe. If you want to run an end-to-end test to verify etwas's overall operation,

* start etwass.exe
* start etwas.exe with something like `etwas --source Ping --sink stdout --sink http://localhost:8080/`
* connect to [http://localhost:8080/](http://localhost:8080/) with your browser
* start ping.exe
* You should now see a new Ping event arriving in your browser at about 1 event/s.

Full details on how to control Ping:

    $ Ping.exe --help

            --rate <int64>: (Estimated) events published per second. Default: 1 event/s
            --help [-h|/h|/help|/?]: display this list of options.



## Maintainer(s)

- Frank Shearar ([frank.shearar@gmail.com](mailto:frank.shearar@gmail.com))

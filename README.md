# Azmon

Azmon provides a clean, configurable way to publish Event Tracing for Windows (ETW) events to remote locations.

Azmon is primarily intended for monitoring machines in an Azure deployment, but should work fine on any Windows machine.

On every machine in your deployment, run `azmon.exe`. On your aggregators (you may have as many as you like) run `azmons.exe`.

To see the events in your aggregators, either connect to one through your browser or point `azmonc.exe` to one. `azmonc` will
dump events to the console.

## How to use it

    $ azmon.exe
    
            --source <string>: Publish events from a named ETW event source. Allowed: HTTP URIs, 'stdout', 'clr'. May occur multiple times
            --sink <string>: Only support HTTP URLs at the moment, or "stdout". No sources means logging to stdout. May occur multiple times.
            --stop: Stop listening to events (affects ALL running azmon processes). If present, other parameters are ignored.
            --help [-h|/h|/help|/?]: display this list of options.

    $ azmons.exe
    
           --port <int>: Listen on a particular port. Defaults to 8080
           --help [-h|/h|/help|/?]: display this list of options.

    $ src/azmonc/bin/Debug/azmonc --help
    
            --server <string>: HTTP/S URI of Azmon server
            --help [-h|/h|/help|/?]: display this list of options.

## TODO

* Keep azmon.exe running when azmons.exe quits. (Or, handle server disconnects gracefully.)
* Remove hardcoded "event" SignalR hub in the client.
* UI, so you can see events in your browser.
* "Raw" connection for console your own sinks off the aggregated logging.
* Privilege separation, so that setting up the ETW trace session can (must) be done elevated, but the actual monitoring can be unprivileged.
* tracking CPU usage, and other system statuses. (Use a statsd-like format? Lower bandwidth requirements than XML events.)
* tracking _per process_ CPU usage, possibly on an opt-in basis. (Use a statsd-like format? Lower bandwidth requirements than XML events.)
* Make the tests more robust, especially in NCrunch.

## Technical details

Azmon starts an ETW trace session, and whenever it sees events, uses [SignalR](http://signalr.net/) to publish events to the aggregators.

In turn the aggregators use SignalR to publish these chunks of XML to your browser, or any other kind of connected client.

## Testing

Azmon also contains a load/smoke test event producer, Ping.exe. If you want to run an end-to-end test to verify azmon's overall operation,

* start azmons.exe
* start azmon.exe with something like `azmon --source Ping --sink stdout --sink http://localhost:8080/`
* connect to [http://localhost:8080/](http://localhost:8080/) with your browser
* start ping.exe
* You should now see a new Ping event arriving in your browser at about 1 event/s.

Full details on how to control Ping:

    $ Ping.exe --help
    
            --rate <int64>: (Estimated) events published per second. Default: 1 event/s
            --help [-h|/h|/help|/?]: display this list of options.



## Maintainer(s)

- Frank Shearar ([frsheara@microsoft.com](mailto:frsheara@microsoft.com))

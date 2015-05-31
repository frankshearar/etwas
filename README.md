# Azmon

Azmon provides a clean, configurable way to publish Event Tracing for Windows (ETW) events to remote locations.

Azmon is primarily intended for monitoring machines in an Azure deployment, but should work fine on any Windows machine.

On every machine in your deployment, run `azmon.exe`. On your aggregators (you may have as many as you like) run `azmons.exe`.

## How to use it

    $ azmon.exe
    
            --source <string>: Publish events from a named ETW event source. May occur multiple times
            --sink <string>: Only support HTTP URLs at the moment, or "stdout". No sources means logging to stdout. May occur multiple times.
            --stop: Stop listening to events (affects ALL running azmon processes). If present, other parameters are ignored.
            --help [-h|/h|/help|/?]: display this list of options.

    $ azmons.exe
    
           --port <int>: Listen on a particular port. Defaults to 8080
           --help [-h|/h|/help|/?]: display this list of options.

## TODO
* UI, so you can see events in your browser
* "Raw" connection for console your own sinks off the aggregated logging
* Privilege separation, so that setting up the ETW trace session can (must) be done elevated, but the actual monitoring can be unprivileged.
* Exposing CLR events
* tracking CPU usage, and other system statuses
* tracking _per process_ CPU usage, possibly on an opt-in basis.

## Technical details

Azmon starts an ETW trace session, and whenever it sees events, uses [SignalR](http://signalr.net/) to publish events to the aggregators.

In turn the aggregators use SignalR to publish these chunks of XML to your browser, or any other kind of connected client.

## Maintainer(s)

- Frank Shearar (frsheara@microsoft.com)

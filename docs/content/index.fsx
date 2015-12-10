(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

(**
azmon
======================

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The azmon library can be <a href="https://nuget.org/packages/azmon">installed from NuGet</a>:
      <pre>PM> Install-Package azmon</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Azmon provides a clean, configurable way to publish Event Tracing for Windows (ETW) events to remote locations.

Azmon is primarily intended for monitoring machines in an Azure deployment, but should work fine on any Windows machine.

On every machine in your deployment, run `azmon.exe`. On your aggregators (you may have as many as you like) run `azmons.exe`.

To see the events in your aggregators, either connect to one through your browser or point `azmonc.exe` to one. `azmonc` will
dump events to the console.

## How to use it

    $ azmon.exe

            --source <string>: Publish events from a named ETW event source. Allowed: event provider names, 'stdout', 'clr'. May occur multiple times
            --sink <string>: 'HTTP URLs, or 'role:InstanceName' for Azure roles, or 'stdout'. No sources means logging to stdout. May occur multiple times.'
            --stop: Stop listening to events (affects ALL running azmon processes). If present, other parameters are ignored.
            --help [-h|/h|/help|/?]: display this list of options.

    $ azmons.exe

           --port <int>: Listen on a particular port. Defaults to 8080
           --help [-h|/h|/help|/?]: display this list of options.

    $ src/azmonc/bin/Debug/azmonc --help

            --server <string>: HTTP/S URI of Azmon server
            --help [-h|/h|/help|/?]: display this list of options.

Some more info

Samples & documentation
-----------------------

The library comes with comprehensible documentation.
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content].
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork
the project and submit pull requests. If you're adding a new public API, please also
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under the MIT license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the
[License file][license] in the GitHub repository.

  [content]: https://github.com/fsprojects/azmon/tree/master/docs/content
  [gh]: https://github.com/fsprojects/azmon
  [issues]: https://github.com/fsprojects/azmon/issues
  [readme]: https://github.com/fsprojects/azmon/blob/master/README.md
  [license]: https://github.com/fsprojects/azmon/blob/master/LICENSE.txt
*)

# Control structure lives in C#; Stylesheets reach Control internals only through Parts

Stylesheets only style; they never create or replace Elements, and no JSON document can replace a Control's Part tree. Each Control builds a fixed Part tree in C#, and Stylesheets reach those Parts with `::part(name)` Selectors, like CSS shadow parts. We rejected WPF-style replaceable templates in Stylesheets because they would turn the Stylesheet format into a second markup language with its own tooling burden, and would make Controls' internals a public contract a Theme could break.

## Consequences

Apps that need a different Control structure subclass the Control or supply a template factory in C#. App-level Element trees may also be defined as Views (ADR 0006), but a View can only compose Controls, never redefine a Control's Part tree.

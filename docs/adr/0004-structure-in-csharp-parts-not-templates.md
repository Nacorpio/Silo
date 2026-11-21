# Element structure lives in C#; Stylesheets reach inside Controls only through Parts

Stylesheets only style; they never create or replace Elements. Each Control builds a fixed Part tree in C#, and Stylesheets reach those Parts with `::part(name)` Selectors, like CSS shadow parts. We rejected WPF-style replaceable templates in Stylesheets because they would turn the Stylesheet format into a second markup language with its own tooling burden, and would make Controls' internals a public contract a Theme could break.

## Consequences

Apps that need a different structure subclass the Control or supply a template factory in C#. App UI trees are also C# only in v1; any future markup will be a source generator emitting the same C#.

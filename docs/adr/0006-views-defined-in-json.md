# App Element trees can be Views defined in JSON, compiled to the same C#

Alongside C#, an app's Element tree can be written as a View in a View format, JSONC first. This follows ADR 0002's shape: a syntax-independent view model, pluggable View formats, and an optional lossless syntax tree so Silo Studio can edit Views in place. A source generator compiles each View to C#, so Release builds have no parsing cost and stay AOT-safe (ADR 0005). A runtime loader builds the tree through the generated registry of Element factories and property setters, which drives structural hot reload in Debug, Studio editing and loading Views from disk.

We rejected letting JSON redefine a Control's Part tree: Themes rely on Parts being fixed (ADR 0004), and build-time validation of `::part()` Selectors would no longer be possible.

## Consequences

A View declares its data type and bindings are compiled against it (`"text": "{bind Name}"`), emitting the same lambda bindings C# uses; an optional class name generates a `partial class` with fields for named Elements. There is no inline code or expression language in Views. C# and Views are equals: a View can use any registered Element, including custom C# Controls, and C# can create a View like any other type.

# Native AOT and trimming from day one: no reflection

Silo must work under Native AOT and full trimming from the first commit. Styled property metadata, the registry that maps Stylesheet property names to Styled properties, value parsers and data bindings are all produced by source generators; nothing resolves types or members by name at runtime. Retrofitting this onto a reflection-based property system is a rewrite, so we pay the cost up front.

## Consequences

Bindings are lambda-based and compiled (`(MyVm vm) => vm.Name`), not string paths. Plugging in a new Control or Stylesheet format means generated registration code, not assembly scanning.

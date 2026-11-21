# Stylesheets are syntax-independent; JSONC is the first Stylesheet format

The styling engine works on a syntax-independent Stylesheet model (Tokens, Rules, Selectors, values). Each Stylesheet format is a reader that produces that model, so CSS-style syntax or others can be added without touching the Cascade. The first format is JSONC (JSON with comments and trailing commas), read with `System.Text.Json`'s `Utf8JsonReader`: authors get a published JSON Schema for editor completion and validation, tools can generate it trivially, and we avoid writing a parser before we need one.

## Consequences

- Selectors and property values stay CSS-like strings inside the JSON (`"Button.primary:hover"`, `"1px solid #333"`), so their grammars live in the engine and are shared by every Stylesheet format.
- The JSONC reader relies on source order and allows duplicate selector keys (a later key is a later Rule), which a generic JSON-to-object deserializer would lose; it must stay a streaming reader.
- A Stylesheet format may also be *editable*: it then provides a lossless syntax tree that keeps comments, whitespace and ordering, so tools (Silo Studio's text and visual editors, and later Inspector write-back) can change a Stylesheet without reformatting it. The JSONC format implements this first; reading-only formats remain valid.
- Every Declared value carries its source span (file, line, column) in all builds, for Cascade traces and Stylesheet error messages.

# Silo is a full framework with its own renderer

Silo draws every pixel itself (Skia behind a thin drawing abstraction, SDL3 behind a platform abstraction) and owns layout, input and text, rather than being a styling layer over WPF, WinUI, MAUI or Avalonia. A layer would ship sooner, but every host framework caps what Selectors, the Cascade and CSS layout can express; owning the renderer is the only way to make CSS-like styling complete and identical on Windows, macOS and Linux.

## Consequences

Text layout, IME, accessibility and windowing are ours to build. Accessibility is designed into the core (an automation-peer tree) from the start so it is not a later rewrite.

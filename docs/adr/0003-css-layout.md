# Layout is CSS layout, fully styleable

Layout uses the CSS box model with flexbox and grid, and every layout property (`display`, `gap`, `flex-direction`, `grid-template-columns`, margins, padding) is a Styled property set from Stylesheets. We rejected WPF-style panels, where layout is fixed by the tree and only visuals are styleable, because it would make the "CSS-like" promise hollow: a Theme could not rearrange anything.

## Consequences

We implement flex and grid ourselves in C# (AOT-friendly, debuggable), using the Yoga and Taffy test suites as conformance fixtures. Floats, inline formatting flow and general `position: absolute` are out of scope; absolute positioning exists only for overlays such as popups.

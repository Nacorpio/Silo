# Silo

Silo is a cross-platform desktop UI framework whose look and layout are defined by CSS-like Stylesheets. This glossary fixes the words we use for its concepts.

## Language

### Element tree

**Element**:
Any node in the UI tree, such as a Box or a TextBlock.
_Avoid_: Visual, widget, view, node

**Control**:
An Element that owns a Part tree and handles user interaction, such as a Button or a Slider.
_Avoid_: Widget, component

**Part**:
A named Element inside a Control's Part tree, reachable from Stylesheets but defined only in C#.
_Avoid_: Template part, sub-element, child

**Class**:
A free-form label that app code attaches to an Element so Selectors can match it.
_Avoid_: Tag, style key

**Pseudo-state**:
A state the framework or a Control sets on an Element (hover, pressed, checked); each Control declares the Pseudo-states it supports, and app code never sets them.
_Avoid_: Visual state, state

**Styled property**:
A property of an Element that takes part in the Cascade, named in kebab-case in Stylesheets and PascalCase in C#.
_Avoid_: Dependency property, attribute, style property

**Local value**:
A Styled property value set directly on an Element from C#; it beats every Stylesheet value.
_Avoid_: Inline style, override

### Styling

**Stylesheet**:
A set of Tokens and Rules that an app or Theme loads, independent of the syntax it was written in.
_Avoid_: Style file, resource dictionary, skin

**Stylesheet format**:
A concrete syntax that Stylesheets can be written in; JSONC is the first one.
_Avoid_: Style language, dialect, parser

**Rule**:
A Selector paired with the Styled property values and Tokens it sets, possibly with nested Rules.
_Avoid_: Style, setter block

**Selector**:
The pattern in a Rule that decides which Elements the Rule applies to.
_Avoid_: Target, key

**Token**:
A named value declared in a Stylesheet or Rule and inherited down the Element tree, referenced as `var(--name)`.
_Avoid_: Variable, resource, custom property

**Theme**:
A named, ordered set of Stylesheets shipped together, such as the Default theme.
_Avoid_: Skin, style pack

**Theme variant**:
A named set of Token values that overrides a Theme's Tokens, such as Light or Dark.
_Avoid_: Color scheme, mode, sub-theme

### Cascade

**Cascade**:
The ordering that decides which of several competing values an Element's Styled property ends up with.
_Avoid_: Precedence, priority chain

**Cascade layer**:
A named band of Stylesheets inside the app's position in the Cascade; later layers win over earlier ones.
_Avoid_: Tier, level

**Declared value**:
A value that a matching Rule sets for a Styled property.

**Cascaded value**:
The Declared value that wins the Cascade, or a Local value when one is set.

**Computed value**:
The Cascaded value with Tokens resolved and font-relative units converted.

**Used value**:
The Computed value after layout, with container-relative units resolved.

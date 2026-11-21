using Silo.Styling;

namespace Silo;

/// <summary>
/// A node of the retained UI tree. An Element has a type name, an optional name, Classes, Pseudo-states and Styled property
/// values, which is what Selectors match against.
/// </summary>
/// <remarks>
/// <para>
/// The value of a Styled property is the first that exists of: the Local value set in C#, the value from the Stylesheets
/// (applied by <see cref="ApplyStyles"/>), the parent's value when the property inherits, and the property's default.
/// </para>
/// <para>
/// Derived types declare their own Styled properties with <see cref="StyledProperty.Register{TOwner, T}"/> and set Pseudo-states
/// from input handling with <see cref="SetPseudoState"/>. The base type does not set any Pseudo-state itself.
/// </para>
/// <para>
/// A derived type that declares Styled properties must also declare an empty <see langword="static"/> constructor. Without it
/// the runtime may defer the static field initializers until a field is first read, and Stylesheets could not find the
/// properties yet. A source generator will emit this later.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var root = new Element();
/// var child = new Element { Name = "save" };
///
/// child.AddClass("primary");
/// root.Children.Add(child);
///
/// var sheet = JsoncStylesheetReader.Read("""{ "rules": { ".primary": { "opacity": "0.5" } } }""", "app.jsonc");
/// var diagnostics = root.ApplyStyles(new StyleResolver([sheet]));
/// </code>
/// </example>
public class Element : IStyleNode
{
    /// <summary>The opacity of the Element and its subtree, from 0 (transparent) to 1 (opaque).</summary>
    public static readonly StyledProperty<double> OpacityProperty =
        StyledProperty.Register<Element, double>("opacity", 1, TryParseOpacity);

    /// <summary>The Classes of this Element.</summary>
    readonly HashSet<string> classes = new(StringComparer.Ordinal);

    /// <summary>The Pseudo-states that are currently set.</summary>
    readonly HashSet<string> pseudoStates = new(StringComparer.Ordinal);

    /// <summary>Local values set from C#, by property.</summary>
    readonly Dictionary<StyledProperty, object?> localValues = [];

    /// <summary>Values from the last <see cref="ApplyStyles"/>, by property.</summary>
    readonly Dictionary<StyledProperty, object?> styledValues = [];

    /// <summary>
    /// Runs before the first Element is created. The explicit static constructor stops the runtime from running the field
    /// initializers lazily, which would leave <see cref="OpacityProperty"/> unregistered until someone read it.
    /// </summary>
    static Element()
    {
    }

    /// <summary>Creates an Element without a parent.</summary>
    public Element() => Children = new ElementCollection(this);

    /// <summary>Gets or sets the Element's name, matched by <c>#name</c> Selectors. The default is <see langword="null"/>.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets the name that Selectors match as the Element's type. The default is the name of the CLR type.
    /// </summary>
    /// <remarks>Override it when the Stylesheet name should differ from the class name.</remarks>
    public virtual string TypeName => GetType().Name;

    /// <summary>Gets the parent Element, or <see langword="null"/> for a root or an Element that is not in a tree.</summary>
    public Element? Parent { get; private set; }

    /// <summary>Gets the children of this Element. Adding or removing a child updates its <see cref="Parent"/>.</summary>
    public ElementCollection Children { get; }

    /// <summary>Gets the Classes of this Element.</summary>
    public IReadOnlyCollection<string> Classes => classes;

    /// <summary>Gets the Pseudo-states that are currently set, such as <c>hover</c>.</summary>
    public IReadOnlyCollection<string> PseudoStates => pseudoStates;

    /// <inheritdoc/>
    IStyleNode? IStyleNode.Parent => Parent;

    /// <summary>Adds a Class.</summary>
    /// <param name="name">The Class name, without the leading dot.</param>
    /// <returns><see langword="true"/> when the Class was added, and <see langword="false"/> when it was already there.</returns>
    public bool AddClass(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return classes.Add(name);
    }

    /// <summary>Removes a Class.</summary>
    /// <param name="name">The Class name, without the leading dot.</param>
    /// <returns><see langword="true"/> when the Class was removed.</returns>
    public bool RemoveClass(string name) => classes.Remove(name);

    /// <summary>Tests whether the Element has a Class.</summary>
    /// <param name="name">The Class name, without the leading dot.</param>
    /// <returns><see langword="true"/> when the Class is set.</returns>
    public bool HasClass(string name) => classes.Contains(name);

    /// <summary>
    /// Sets or clears a Pseudo-state. Pseudo-states describe the Element's interaction state and are set by the framework, not
    /// by application code.
    /// </summary>
    /// <param name="name">The Pseudo-state name, without the colon, for example <c>hover</c>.</param>
    /// <param name="value"><see langword="true"/> to set the state and <see langword="false"/> to clear it.</param>
    /// <returns><see langword="true"/> when the state changed, so that styles need to be applied again.</returns>
    protected bool SetPseudoState(string name, bool value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return value ? pseudoStates.Add(name) : pseudoStates.Remove(name);
    }

    /// <summary>
    /// Gets the value of a Styled property.
    /// </summary>
    /// <typeparam name="T">The value type of the property.</typeparam>
    /// <param name="property">The property to read.</param>
    /// <returns>
    /// The Local value if there is one, otherwise the value from the Stylesheets, otherwise the parent's value when the
    /// property inherits, otherwise the property's default.
    /// </returns>
    public T GetValue<T>(StyledProperty<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return (T)GetObject(property)!;
    }

    /// <summary>
    /// Sets the Local value of a Styled property, which beats every Stylesheet.
    /// </summary>
    /// <typeparam name="T">The value type of the property.</typeparam>
    /// <param name="property">The property to set.</param>
    /// <param name="value">The new Local value.</param>
    public void SetValue<T>(StyledProperty<T> property, T value)
    {
        ArgumentNullException.ThrowIfNull(property);

        var before = GetObject(property);

        localValues[property] = value;

        NotifyIfChanged(property, before);
    }

    /// <summary>
    /// Removes the Local value of a Styled property, so that the Stylesheets and defaults apply again.
    /// </summary>
    /// <param name="property">The property to clear.</param>
    /// <returns><see langword="true"/> when there was a Local value.</returns>
    public bool ClearValue(StyledProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        var before = GetObject(property);
        var removed = localValues.Remove(property);

        NotifyIfChanged(property, before);

        return removed;
    }

    /// <summary>
    /// Applies the Stylesheets to this Element and all of its descendants.
    /// </summary>
    /// <param name="resolver">The resolver that holds the Stylesheets in load order.</param>
    /// <returns>
    /// The problems found, in tree order. The list is empty when every Declaration that matched could be applied.
    /// </returns>
    /// <remarks>
    /// Values from an earlier call are replaced, so call it again after Classes, Pseudo-states or Stylesheets change.
    /// <see cref="OnPropertyChanged"/> runs for every property whose value changed.
    /// </remarks>
    public IReadOnlyList<StyleDiagnostic> ApplyStyles(StyleResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        var diagnostics = new List<StyleDiagnostic>();

        ApplyStylesCore(resolver, diagnostics);

        return diagnostics;
    }

    /// <summary>
    /// Called after the effective value of a Styled property changed, whatever the reason.
    /// </summary>
    /// <param name="property">The property that changed.</param>
    /// <remarks>
    /// Override it to invalidate layout or rendering. Calling the base method is not required. It runs for the Element that
    /// changed and not for descendants that only inherit the value.
    /// </remarks>
    protected virtual void OnPropertyChanged(StyledProperty property)
    {
    }

    /// <summary>Sets the parent, called by <see cref="ElementCollection"/>.</summary>
    /// <param name="parent">The new parent, or <see langword="null"/>.</param>
    internal void SetParent(Element? parent) => Parent = parent;

    /// <summary>Applies the Stylesheets to this Element, then to its children.</summary>
    /// <param name="resolver">The resolver.</param>
    /// <param name="diagnostics">Receives the problems found.</param>
    void ApplyStylesCore(StyleResolver resolver, List<StyleDiagnostic> diagnostics)
    {
        var cascaded = resolver.Cascade(this);
        var tokens = resolver.ResolveTokens(this);
        var properties = StyledProperty.GetProperties(GetType());
        var before = new object?[properties.Count];

        for (var i = 0; i < properties.Count; i++)
        {
            before[i] = GetObject(properties[i]);
        }

        styledValues.Clear();

        foreach (var (name, declared) in cascaded)
        {
            var property = Find(properties, name);

            if (property is null)
            {
                diagnostics.Add(new StyleDiagnostic(this, name, declared.Value, declared.Source.Span, $"{TypeName} has no Styled property '{name}'."));

                continue;
            }

            string computed;

            try
            {
                computed = StyleResolver.Compute(declared.Value, tokens);
            }
            catch (StylesheetException e)
            {
                diagnostics.Add(new StyleDiagnostic(this, name, declared.Value, declared.Source.Span, e.Message));

                continue;
            }

            if (!property.TryParseObject(computed, out var value))
            {
                diagnostics.Add(new StyleDiagnostic(this, name, declared.Value, declared.Source.Span, $"'{computed}' is not a valid {property.ValueType.Name}."));

                continue;
            }

            styledValues[property] = value;
        }

        for (var i = 0; i < properties.Count; i++)
        {
            NotifyIfChanged(properties[i], before[i]);
        }

        foreach (var child in Children)
        {
            child.ApplyStylesCore(resolver, diagnostics);
        }
    }

    /// <summary>Gets the effective boxed value of a property.</summary>
    /// <param name="property">The property to read.</param>
    /// <returns>The value after Local value, Stylesheet value, inheritance and default are considered.</returns>
    object? GetObject(StyledProperty property)
    {
        if (localValues.TryGetValue(property, out var local))
        {
            return local;
        }

        if (styledValues.TryGetValue(property, out var styled))
        {
            return styled;
        }

        if (property.Inherits && Parent is not null)
        {
            return Parent.GetObject(property);
        }

        return property.DefaultObject;
    }

    /// <summary>Raises <see cref="OnPropertyChanged"/> when the effective value differs from an earlier one.</summary>
    /// <param name="property">The property that may have changed.</param>
    /// <param name="before">The effective value before the change.</param>
    void NotifyIfChanged(StyledProperty property, object? before)
    {
        if (!Equals(before, GetObject(property)))
        {
            OnPropertyChanged(property);
        }
    }

    /// <summary>Finds a property by name in a list.</summary>
    /// <param name="properties">The properties to search.</param>
    /// <param name="name">The kebab-case name.</param>
    /// <returns>The property, or <see langword="null"/>.</returns>
    static StyledProperty? Find(IReadOnlyList<StyledProperty> properties, string name)
    {
        foreach (var property in properties)
        {
            if (property.Name == name)
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>Parses an opacity, accepting a number or a percentage.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="value">The opacity clamped to 0..1.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    static bool TryParseOpacity(string text, out double value)
    {
        text = text.Trim();

        var percent = text.EndsWith('%');

        if (double.TryParse(percent ? text[..^1] : text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number))
        {
            value = Math.Clamp(percent ? number / 100 : number, 0, 1);

            return true;
        }

        value = 0;

        return false;
    }
}

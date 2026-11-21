using System.Collections.Concurrent;

namespace Silo;

/// <summary>
/// Parses the text of a Declared value, as written in a Stylesheet, into a typed value.
/// </summary>
/// <typeparam name="T">The type of the parsed value.</typeparam>
/// <param name="text">The Computed text of the value, with every <c>var()</c> reference already resolved.</param>
/// <param name="value">The parsed value when the method returns <see langword="true"/>; otherwise the default of <typeparamref name="T"/>.</param>
/// <returns><see langword="true"/> when <paramref name="text"/> is a valid value; otherwise <see langword="false"/>.</returns>
/// <remarks>
/// The value parsers of Silo.Styling, such as <c>Length.TryParse</c>, already have this shape and can be passed as method groups.
/// </remarks>
public delegate bool ValueParser<T>(string text, out T value);

/// <summary>
/// Identifies a Styled property: a property of an Element that Stylesheets can set by name.
/// </summary>
/// <remarks>
/// <para>
/// A Styled property has a kebab-case name in Stylesheets and a PascalCase CLR property in C#. Register it once with
/// <see cref="Register{TOwner, T}"/>, usually in a static field of the owning Element type, and read or write it with
/// <see cref="Element.GetValue{T}"/> and <see cref="Element.SetValue{T}"/>.
/// </para>
/// <para>
/// Registration is not reflection based, so it works under Native AOT. A property is only known once its owner type's static
/// constructor has run, which the runtime does before the first instance of the type is created. Looking a type up before
/// any instance exists may therefore miss its properties; this keeps registration free of reflection for Native AOT.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// public class Box : Element
/// {
///     public static readonly StyledProperty&lt;Color&gt; BackgroundProperty =
///         StyledProperty.Register&lt;Box, Color&gt;("background", default, Color.TryParse);
///
///     public Color Background
///     {
///         get => GetValue(BackgroundProperty);
///         set => SetValue(BackgroundProperty, value);
///     }
/// }
/// </code>
/// </example>
public abstract class StyledProperty
{
    /// <summary>The properties registered per owner type, in registration order.</summary>
    static readonly ConcurrentDictionary<Type, List<StyledProperty>> Registry = new();

    /// <summary>Initializes the shared state of a Styled property.</summary>
    /// <param name="name">The kebab-case name used in Stylesheets.</param>
    /// <param name="ownerType">The Element type that declares the property.</param>
    /// <param name="valueType">The type of the property's value.</param>
    /// <param name="inherits">Whether an unset value is taken from the parent Element.</param>
    private protected StyledProperty(string name, Type ownerType, Type valueType, bool inherits)
    {
        Name = name;
        OwnerType = ownerType;
        ValueType = valueType;
        Inherits = inherits;
    }

    /// <summary>Gets the name of the property as written in Stylesheets, in kebab-case.</summary>
    public string Name { get; }

    /// <summary>Gets the Element type that declared the property.</summary>
    public Type OwnerType { get; }

    /// <summary>Gets the type of the property's value.</summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets a value indicating whether an Element without a value of its own takes the value of its parent.
    /// </summary>
    /// <remarks>Only a small, whitelisted set of properties inherits, such as text color and font settings.</remarks>
    public bool Inherits { get; }

    /// <summary>Gets the default value as an <see cref="object"/>, boxed when the value type is a struct.</summary>
    internal abstract object? DefaultObject { get; }

    /// <summary>Parses text into a boxed value with the property's parser.</summary>
    /// <param name="text">The Computed text to parse.</param>
    /// <param name="value">The parsed, boxed value on success.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    internal abstract bool TryParseObject(string text, out object? value);

    /// <summary>
    /// Registers a Styled property for an owner type.
    /// </summary>
    /// <typeparam name="TOwner">The Element type that declares the property.</typeparam>
    /// <typeparam name="T">The type of the property's value.</typeparam>
    /// <param name="name">The kebab-case name used in Stylesheets, for example <c>"background-color"</c>.</param>
    /// <param name="defaultValue">The value used when neither a Local value, a Stylesheet nor inheritance provides one.</param>
    /// <param name="parser">Parses the Computed text of a Declaration into a value.</param>
    /// <param name="inherits">Whether an unset value is taken from the parent Element. The default is <see langword="false"/>.</param>
    /// <returns>The new property, to be stored in a <see langword="static readonly"/> field.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, or <typeparamref name="TOwner"/> already declares a property with that name.
    /// </exception>
    public static StyledProperty<T> Register<TOwner, T>(string name, T defaultValue, ValueParser<T> parser, bool inherits = false)
        where TOwner : Element
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(parser);

        var property = new StyledProperty<T>(name, typeof(TOwner), defaultValue, parser, inherits);
        var list = Registry.GetOrAdd(typeof(TOwner), static _ => []);

        lock (list)
        {
            if (list.Exists(p => p.Name == name))
            {
                throw new ArgumentException($"{typeof(TOwner).Name} already declares a Styled property named '{name}'.", nameof(name));
            }

            list.Add(property);
        }

        return property;
    }

    /// <summary>
    /// Gets the Styled properties that an Element type has, including those it inherits from its base types.
    /// </summary>
    /// <param name="type">An <see cref="Element"/> type.</param>
    /// <returns>
    /// The properties of the most derived type first. A property of a derived type hides a base type property of the same name.
    /// Only properties whose owner type has been initialized are included; see the remarks on <see cref="StyledProperty"/>.
    /// </returns>
    public static IReadOnlyList<StyledProperty> GetProperties(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var result = new List<StyledProperty>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = type; current is not null && typeof(Element).IsAssignableFrom(current); current = current.BaseType)
        {
            if (!Registry.TryGetValue(current, out var list))
            {
                continue;
            }

            lock (list)
            {
                foreach (var property in list)
                {
                    if (seen.Add(property.Name))
                    {
                        result.Add(property);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds a Styled property of an Element type by its Stylesheet name.
    /// </summary>
    /// <param name="type">An <see cref="Element"/> type.</param>
    /// <param name="name">The kebab-case name to look for.</param>
    /// <returns>The property, or <see langword="null"/> when the type has no property with that name.</returns>
    public static StyledProperty? Find(Type type, string name)
    {
        foreach (var property in GetProperties(type))
        {
            if (property.Name == name)
            {
                return property;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{OwnerType.Name}.{Name}";
}

/// <summary>
/// A <see cref="StyledProperty"/> with a known value type.
/// </summary>
/// <typeparam name="T">The type of the property's value.</typeparam>
public sealed class StyledProperty<T> : StyledProperty
{
    /// <summary>The parser for Stylesheet text.</summary>
    readonly ValueParser<T> parser;

    /// <summary>Creates the property. Use <see cref="StyledProperty.Register{TOwner, T}"/> instead.</summary>
    internal StyledProperty(string name, Type ownerType, T defaultValue, ValueParser<T> parser, bool inherits)
        : base(name, ownerType, typeof(T), inherits)
    {
        DefaultValue = defaultValue;
        this.parser = parser;
    }

    /// <summary>Gets the value used when nothing else provides one.</summary>
    public T DefaultValue { get; }

    /// <summary>Parses Stylesheet text into a typed value.</summary>
    /// <param name="text">The Computed text to parse.</param>
    /// <param name="value">The parsed value on success.</param>
    /// <returns><see langword="true"/> when the text is valid.</returns>
    public bool TryParse(string text, out T value) => parser(text, out value);

    /// <inheritdoc/>
    internal override object? DefaultObject => DefaultValue;

    /// <inheritdoc/>
    internal override bool TryParseObject(string text, out object? value)
    {
        if (parser(text, out var typed))
        {
            value = typed;

            return true;
        }

        value = null;

        return false;
    }
}

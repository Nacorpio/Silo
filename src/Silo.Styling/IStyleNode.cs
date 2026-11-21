namespace Silo.Styling;

/// <summary>
/// The view of an Element that the styling engine needs. <see cref="Selector"/> matching and <see cref="StyleResolver"/> read
/// everything they know about the Element tree through this interface, so Silo.Styling has no dependency on the Element
/// types themselves and can be tested without a window.
/// </summary>
/// <remarks>
/// <para>
/// Implement it on the type that represents an Element, or on a thin adapter. The members are read many times while a
/// Selector is matched, so they should be cheap and should not allocate.
/// </para>
/// <para>
/// All name comparisons the engine makes are ordinal and case-sensitive.
/// </para>
/// </remarks>
/// <example>
/// A minimal implementation, as used by tests:
/// <code language="csharp">
/// sealed class Node(string type, IStyleNode? parent = null) : IStyleNode
/// {
///     public string TypeName => type;
///     public string? Name => null;
///     public IReadOnlyCollection&lt;string&gt; Classes => ["primary"];
///     public IReadOnlyCollection&lt;string&gt; PseudoStates => [];
///     public IStyleNode? Parent => parent;
/// }
///
/// var button = new Node("Button", new Node("Box"));
/// Selector.Parse("Box > Button.primary").Matches(button); // true
/// </code>
/// </example>
public interface IStyleNode
{
    /// <summary>
    /// The name of the Element's type as a Selector writes it, for example <c>"Button"</c>. A type Selector matches this
    /// value exactly; derived types do not match the name of their base type.
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// The Element's name, matched by an <c>#name</c> Selector, or <see langword="null"/> if it has none. Names are meant to
    /// be unique within a View but the engine does not check that.
    /// </summary>
    string? Name { get; }

    /// <summary>
    /// The Classes that app code has attached to the Element, matched by <c>.class</c> Selectors. The collection is empty,
    /// never <see langword="null"/>, when there are none.
    /// </summary>
    IReadOnlyCollection<string> Classes { get; }

    /// <summary>
    /// The Pseudo-states that the framework or the Control currently sets on the Element, such as <c>"hover"</c>, without the
    /// leading colon. A <c>:hover</c> Selector matches while the collection contains <c>"hover"</c>. App code never sets
    /// these.
    /// </summary>
    IReadOnlyCollection<string> PseudoStates { get; }

    /// <summary>
    /// The Element's parent in the Element tree, or <see langword="null"/> for the root. Descendant and child combinators and
    /// Token inheritance walk up through this property.
    /// </summary>
    IStyleNode? Parent { get; }
}

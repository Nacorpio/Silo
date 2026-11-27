namespace Silo;

/// <summary>
/// Declares the Pseudo-states that a Control type supports: the ones it sets on itself and that a Stylesheet can match, such as
/// <c>hover</c> or <c>checked</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only tools read it: the Stylesheet validator knows from it which <c>:state</c> names exist, and Silo Studio's theme matrix
/// shows one column for each state that applies to a Control. The runtime never does, so it does not need reflection.
/// </para>
/// <para>
/// A declaration replaces the one of the base type instead of adding to it, so list every state the type supports. A type
/// without a declaration has the one of its nearest base type that has one.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// [PseudoStates("hover", "active", "focus")]
/// public class Button : TextControl
/// </code>
/// </example>
/// <param name="states">The Pseudo-state names, as written in Selectors: <c>hover</c>, <c>active</c>, <c>focus</c>, <c>checked</c> and so on.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PseudoStatesAttribute(params string[] states) : Attribute
{
    /// <summary>Gets the Pseudo-state names.</summary>
    public IReadOnlyList<string> States { get; } = states;
}

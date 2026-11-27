namespace Silo;

/// <summary>
/// A pointer position and the modifier keys that were held, as the <see cref="PointerRouter"/> hands them to an
/// <see cref="IPointerHandler"/>.
/// </summary>
/// <remarks>
/// The position is relative to the border box of the Control that receives the event, so it can be negative or past the size of
/// the Control while a drag is going on.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var e = new PointerEvent(12, 8, KeyModifiers.Shift);
/// </code>
/// </example>
/// <param name="X">The horizontal position relative to the Control's border box.</param>
/// <param name="Y">The vertical position relative to the Control's border box.</param>
/// <param name="Modifiers">The modifier keys that were held. The default is none.</param>
public readonly record struct PointerEvent(double X, double Y, KeyModifiers Modifiers = KeyModifiers.None);

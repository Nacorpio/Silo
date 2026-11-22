namespace Silo;

/// <summary>
/// Marks a method that Silo Studio can show: a <see langword="static"/> method without parameters that returns the Element to
/// display, such as a Control with sample data.
/// </summary>
/// <remarks>
/// <para>
/// Studio loads the assembly in a child process, runs the method and shows what it returns in a window with the Default
/// theme and the theme file being edited. When the method returns a <see cref="Window"/> it is shown as it is, and any other
/// Element is put in a Window that fills the client area.
/// </para>
/// <para>The method may be public or not, and may be in any type. A Preview that throws is reported instead of shown.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// static class ButtonPreviews
/// {
///     [Preview("Primary button")]
///     public static Element Primary() => new Button { Text = "Save" };
/// }
/// </code>
/// </example>
/// <param name="name">The name that Studio lists, or <see langword="null"/> to use the name of the method.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PreviewAttribute(string? name = null) : Attribute
{
    /// <summary>Gets the name that Studio lists, or <see langword="null"/> for the name of the method.</summary>
    public string? Name { get; } = name;
}

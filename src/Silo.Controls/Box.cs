namespace Silo;

/// <summary>
/// A plain container: it paints its background and border, and lays out its children with flexbox.
/// </summary>
/// <remarks>Box adds nothing to <see cref="Control"/>. Style it with a type, a Class or a name, like any other Control.</remarks>
/// <example>
/// <code language="csharp">
/// var toolbar = new Box { Name = "toolbar" };
///
/// toolbar.AddClass("row");
/// toolbar.Children.Add(new Button { Text = "Save" });
/// toolbar.Children.Add(new Button { Text = "Cancel" });
/// </code>
/// </example>
public class Box : Control
{
}

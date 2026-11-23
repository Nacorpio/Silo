namespace Silo;

/// <summary>A thin row for status information, with an area at the left, one in the middle and one at the right.</summary>
/// <remarks>
/// <para>
/// The three areas, <see cref="Left"/>, <see cref="Center"/> and <see cref="Right"/>, are <see cref="Box"/>es with the Classes
/// <c>left</c>, <c>center</c> and <c>right</c>; add any Control to them, or use <see cref="AddItem"/> for a text. The StatusBar
/// does not dock itself: put it at the bottom of your layout.
/// </para>
/// <para>The Default theme styles it like a Toolbar turned over, with a top border and small text, and centers the middle area.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var status = new StatusBar();
///
/// status.AddItem("Ready");
/// status.AddItem("Ln 12, Col 4", status.Right);
/// </code>
/// </example>
public class StatusBar : Control
{
    /// <summary>
    /// Runs before the first StatusBar is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static StatusBar()
    {
    }

    /// <summary>Creates a StatusBar with three empty areas.</summary>
    public StatusBar()
    {
        Left.AddClass("left");
        Center.AddClass("center");
        Right.AddClass("right");

        Children.Add(Left);
        Children.Add(Center);
        Children.Add(Right);
    }

    /// <summary>Gets the area at the left.</summary>
    public Box Left { get; } = new();

    /// <summary>Gets the area in the middle.</summary>
    public Box Center { get; } = new();

    /// <summary>Gets the area at the right.</summary>
    public Box Right { get; } = new();

    /// <summary>Adds a text to an area.</summary>
    /// <param name="text">The text.</param>
    /// <param name="area">The area to add it to, one of <see cref="Left"/>, <see cref="Center"/> and <see cref="Right"/>. The default is the left one.</param>
    /// <returns>The TextBlock, which has the Class <c>item</c>.</returns>
    public TextBlock AddItem(string text, Box? area = null)
    {
        var item = new TextBlock { Text = text };

        item.AddClass("item");
        (area ?? Left).Children.Add(item);

        return item;
    }
}

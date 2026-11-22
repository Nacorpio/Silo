using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>The title of one menu in a <see cref="MenuBar"/>.</summary>
/// <remarks>
/// <para>
/// A MenuBarItem has the <c>open</c> Pseudo-state while its menu is shown, so a Stylesheet can restyle it with
/// <c>MenuBarItem:open</c>. Style it with the <c>MenuBarItem</c> type.
/// </para>
/// <para>
/// When the title was added with an access key, such as <c>&amp;File</c>, the letter is underlined, and Alt with that letter
/// opens the menu. See <see cref="MenuBar.AccessKeyDown"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var bar = new MenuBar();
/// var file = bar.AddMenu("&amp;File", [new MenuItem("&amp;Open", Open), new MenuItem("E&amp;xit", Exit)]);
///
/// file.AddClass("primary");
/// </code>
/// </example>
public sealed class MenuBarItem : Button
{
    /// <summary>Whether the menu is shown.</summary>
    bool isOpen;

    /// <summary>Creates a title with a menu.</summary>
    /// <param name="menu">The menu that opens below the title.</param>
    /// <param name="title">The text of the title, with an ampersand in front of the access key.</param>
    internal MenuBarItem(ContextMenu menu, string title)
    {
        Menu = menu;

        var (text, index, key) = AccessKeys.Parse(title);

        Text = text;
        AccessIndex = index;
        AccessKey = key;
    }

    /// <summary>Gets the menu that opens below the title.</summary>
    public ContextMenu Menu { get; }

    /// <summary>Gets the bar that the title is in, or <see langword="null"/> when it is not in one.</summary>
    public MenuBar? Bar => Parent as MenuBar;

    /// <summary>Gets the access key as an upper-case letter, or <see langword="null"/> when the title has none.</summary>
    public char? AccessKey { get; }

    /// <summary>Gets whether the menu is shown. Styles must be applied again after it changes.</summary>
    public bool IsOpen
    {
        get => isOpen;
        internal set
        {
            isOpen = value;

            SetPseudoState("open", value);
        }
    }

    /// <summary>Gets the index of the access key in <see cref="TextControl.Text"/>, or -1.</summary>
    int AccessIndex { get; }

    /// <inheritdoc/>
    protected override bool OnClick()
    {
        Bar?.Toggle(this);

        return true;
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        if (AccessIndex < 0)
            return;

        var style = CurrentTextStyle();
        var measurer = Measurer();
        var (x, y) = TextPosition(bounds);

        var before = measurer.Measure(Text[..AccessIndex], style).Width;
        var key = measurer.Measure(Text[AccessIndex].ToString(), style);

        context.FillRect(new Rect(x + before, y + key.Height - 1, key.Width, 1), GetValue(ColorProperty));
    }
}

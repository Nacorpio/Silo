using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>A modal box with a title, content and buttons that opens in the middle of the window and blocks what is behind it.</summary>
/// <remarks>
/// <para>
/// Open it with <see cref="Show"/>. While it is open the rest of the window is dimmed with <c>backdrop-color</c> and does not
/// react to the pointer, and a press on the dimmed area does nothing. Pressing a button closes the Dialog and passes the
/// button's result to <see cref="Closed"/>. Escape dismisses it, which closes it with <see cref="DialogClosedEventArgs.IsDismissed"/>.
/// </para>
/// <para>
/// A Dialog stacks its <see cref="TitleControl"/>, its <see cref="Content"/> and its <see cref="ButtonRow"/>, leaving out the
/// ones that are empty. Style it with the <c>Dialog</c>, <c>DialogTitle</c> and <c>DialogButtons</c> types. Its buttons are
/// ordinary <see cref="Button"/>s.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var dialog = new Dialog { Title = "Delete file", Content = new TextBlock { Text = "This cannot be undone." } };
///
/// dialog.AddButton("Cancel", "cancel");
/// dialog.AddButton("Delete", "delete");
///
/// dialog.Closed += (_, e) => files.Delete(e.Result as string == "delete");
/// dialog.Show(window.Root);
/// </code>
/// </example>
public class Dialog : Control
{
    /// <summary>The color that dims the window behind the Dialog.</summary>
    public static readonly StyledProperty<Color> BackdropColorProperty =
        StyledProperty.Register<Dialog, Color>("backdrop-color", new(0, 0, 0, 102), Color.TryParse);


    /// <summary>The title.</summary>
    readonly DialogTitle title = new();

    /// <summary>The row of buttons.</summary>
    readonly DialogButtons buttons = new();

    /// <summary>The content.</summary>
    Control? content;


    /// <summary>
    /// Runs before the first Dialog is created, so that the Styled property above is registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Dialog()
    {
    }

    /// <summary>Raised after the Dialog closed, whether by a button or because it was dismissed.</summary>
    public event EventHandler<DialogClosedEventArgs>? Closed;

    /// <summary>Gets or sets the title. An empty title is not shown.</summary>
    public string Title
    {
        get => title.Text;
        set
        {
            title.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets the title, so that code can add a Class to it.</summary>
    public DialogTitle TitleControl => title;

    /// <summary>Gets the row of buttons, so that code can add a Class to it.</summary>
    public DialogButtons ButtonRow => buttons;

    /// <summary>Gets or sets what is shown between the title and the buttons, or <see langword="null"/> for nothing.</summary>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;

            Rebuild();
        }
    }

    /// <summary>Gets whether the Dialog is shown.</summary>
    public bool IsOpen => Overlays?.Contains(this) == true;

    /// <summary>Adds a button that closes the Dialog.</summary>
    /// <param name="text">The text on the button.</param>
    /// <param name="result">What <see cref="DialogClosedEventArgs.Result"/> is when this button closes the Dialog. The default is the text.</param>
    /// <returns>The button, so that code can add a Class to it.</returns>
    public Button AddButton(string text, object? result = null)
    {
        var button = new Button { Text = text };

        button.Click += (_, _) => Close(result ?? text);

        buttons.Children.Add(button);

        Rebuild();

        return button;
    }

    /// <summary>Opens the Dialog in the middle of the window.</summary>
    /// <param name="anchor">A Control in the window, which tells the Dialog which window to open in.</param>
    /// <returns><see langword="true"/> when it opened. It does not when it is open already or the anchor is not in a window.</returns>
    public bool Show(Control anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (IsOpen || anchor.Overlays is not { } layer)
            return false;

        Overlays = layer;

        layer.ShowDialog(this, () => Close(null));

        return true;
    }

    /// <summary>Closes the Dialog.</summary>
    /// <param name="result">The result to report, or <see langword="null"/> to report that it was dismissed.</param>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close(object? result = null)
    {
        if (Overlays?.Close(this) != true)
            return false;

        Closed?.Invoke(this, new DialogClosedEventArgs(result));

        return true;
    }

    /// <summary>Puts the title, the content and the button row in the tree, leaving out the empty ones.</summary>
    void Rebuild()
    {
        Children.Clear();

        if (title.Text.Length > 0)
            Children.Add(title);

        if (content is not null)
            Children.Add(content);

        if (buttons.Children.Count > 0)
            Children.Add(buttons);
    }
}

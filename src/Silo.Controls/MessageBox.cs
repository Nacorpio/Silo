namespace Silo;

/// <summary>
/// Shows a short message in a <see cref="Dialog"/> with a standard set of buttons and waits for the answer.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Show"/> opens the box and returns a task that completes with the <see cref="DialogResult"/> of the button that was
/// pressed. Escape closes it with the answer that fits the buttons: OK, Cancel when the box has a Cancel button, otherwise No. A text
/// with line breaks is shown on several lines. The icon is an <see cref="Icon"/> with the Class <c>messagebox-icon</c> and one of
/// <c>messagebox-info</c>, <c>messagebox-warning</c>, <c>messagebox-error</c> and <c>messagebox-question</c>, so a theme colors it.
/// </para>
/// <para>Use <see cref="Create"/> to get the Dialog without showing it, for example to style or preview it.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var answer = await MessageBox.Show(window.Root, "Delete the file?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
///
/// if (answer == DialogResult.Yes)
///     files.Delete(path);
/// </code>
/// </example>
public static class MessageBox
{
    /// <summary>Shows a message and waits for the answer.</summary>
    /// <param name="anchor">A Control in the window, which tells the box which window to open in.</param>
    /// <param name="text">The message. Line breaks start new lines.</param>
    /// <param name="title">The title, or an empty text for none.</param>
    /// <param name="buttons">The buttons. The default is OK.</param>
    /// <param name="icon">The picture. The default is none.</param>
    /// <returns>The button that was pressed. It is <see cref="DialogResult.None"/> when the box could not be shown.</returns>
    public static Task<DialogResult> Show(
        Control anchor,
        string text,
        string title = "",
        MessageBoxButtons buttons = MessageBoxButtons.Ok,
        MessageBoxIcon icon = MessageBoxIcon.None)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var source = new TaskCompletionSource<DialogResult>();
        var dialog = Create(text, title, buttons, icon);

        dialog.Closed += (_, e) => source.TrySetResult(e.Result as DialogResult? ?? Dismissed(buttons));

        if (!dialog.Show(anchor))
            source.TrySetResult(DialogResult.None);

        return source.Task;
    }

    /// <summary>Builds the Dialog of a message without showing it.</summary>
    /// <param name="text">The message. Line breaks start new lines.</param>
    /// <param name="title">The title, or an empty text for none.</param>
    /// <param name="buttons">The buttons.</param>
    /// <param name="icon">The picture.</param>
    /// <returns>The Dialog. Each button's result is its <see cref="DialogResult"/>.</returns>
    public static Dialog Create(string text, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        ArgumentNullException.ThrowIfNull(text);

        var dialog = new Dialog { Title = title ?? string.Empty, Content = Content(text, icon) };

        foreach (var result in Results(buttons))
            dialog.AddButton(Label(result), result);

        return dialog;
    }

    /// <summary>Gets the answer that Escape gives.</summary>
    /// <param name="buttons">The buttons of the box.</param>
    /// <returns>OK for an OK box, Cancel when there is a Cancel button, otherwise No.</returns>
    static DialogResult Dismissed(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.Ok => DialogResult.Ok,
        MessageBoxButtons.YesNo => DialogResult.No,

        _ => DialogResult.Cancel,
    };

    /// <summary>Lists the answers of the buttons in the order they are shown.</summary>
    /// <param name="buttons">The buttons of the box.</param>
    /// <returns>The answers.</returns>
    static DialogResult[] Results(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OkCancel => [DialogResult.Cancel, DialogResult.Ok],
        MessageBoxButtons.YesNo => [DialogResult.No, DialogResult.Yes],
        MessageBoxButtons.YesNoCancel => [DialogResult.Cancel, DialogResult.No, DialogResult.Yes],

        _ => [DialogResult.Ok],
    };

    /// <summary>Gets the text of a button.</summary>
    /// <param name="result">The button's answer.</param>
    /// <returns>The text.</returns>
    static string Label(DialogResult result) => result == DialogResult.Ok ? "OK" : result.ToString();

    /// <summary>Builds the content: the icon, if any, next to the lines of the message.</summary>
    /// <param name="text">The message.</param>
    /// <param name="icon">The picture.</param>
    /// <returns>The content.</returns>
    static Box Content(string text, MessageBoxIcon icon)
    {
        var content = new Box();
        var lines = new Box();

        content.AddClass("messagebox-content");
        lines.AddClass("messagebox-text");

        foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
            lines.Children.Add(new TextBlock { Text = line });

        if (icon != MessageBoxIcon.None)
        {
            var picture = new Icon { Glyph = icon.ToString().ToLowerInvariant() };

            picture.AddClass("messagebox-icon");
            picture.AddClass("messagebox-" + icon.ToString().ToLowerInvariant());

            content.Children.Add(picture);
        }

        content.Children.Add(lines);

        return content;
    }
}

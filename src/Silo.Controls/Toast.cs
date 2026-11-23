using Silo.Rendering;

namespace Silo;

/// <summary>How serious a <see cref="Toast"/> is. The theme colors each kind through its Class.</summary>
public enum ToastSeverity
{
    /// <summary>Something to know. The Class is <c>info</c>. This is the default.</summary>
    Info,

    /// <summary>Something that went well. The Class is <c>success</c>.</summary>
    Success,

    /// <summary>Something that needs attention. The Class is <c>warning</c>.</summary>
    Warning,

    /// <summary>Something that went wrong. The Class is <c>error</c>.</summary>
    Error,
}

/// <summary>
/// A short message that a <see cref="ToastHost"/> shows in a corner of the window for a few seconds.
/// </summary>
/// <remarks>
/// <para>
/// A Toast has a message, and optionally a title, an icon (a mask icon is drawn in the text color), an action button and a
/// close button. Create one with <see cref="ToastHost.Show"/> rather than with the constructor.
/// </para>
/// <para>
/// The look comes from the theme: the Toast has the Class of its <see cref="Severity"/> (<c>info</c>, <c>success</c>,
/// <c>warning</c> or <c>error</c>), and its parts the Classes <c>icon</c>, <c>body</c>, <c>title</c>, <c>message</c>,
/// <c>action</c> and <c>close</c>. Give it a <c>width</c>, since its text does not wrap.
/// </para>
/// <para>Arrange the tree again after changing the text, the icon or the severity.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var toast = window.Toasts.Show("Saved", severity: ToastSeverity.Success, actionText: "Undo");
///
/// toast.ActionInvoked += (_, _) => document.Undo();
/// </code>
/// </example>
public class Toast : Control
{
    /// <summary>The icon.</summary>
    readonly Image iconImage = new();

    /// <summary>The title.</summary>
    readonly TextBlock titleText = new();

    /// <summary>The message.</summary>
    readonly TextBlock messageText = new();

    /// <summary>The Control that holds the title and the message.</summary>
    readonly Box body = new();

    /// <summary>The action button.</summary>
    readonly Button actionButton = new();

    /// <summary>The close button.</summary>
    readonly Button closeButton = new() { Text = "×" };


    /// <summary>The kind of the Toast.</summary>
    ToastSeverity severity = ToastSeverity.Info;

    /// <summary>The icon, or <see langword="null"/>.</summary>
    IImage? icon;

    /// <summary>The title.</summary>
    string title = string.Empty;

    /// <summary>The text of the action button.</summary>
    string actionText = string.Empty;


    /// <summary>
    /// Runs before the first Toast is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Toast()
    {
    }

    /// <summary>Creates a Toast with no message.</summary>
    public Toast()
    {
        iconImage.AddClass("icon");
        body.AddClass("body");
        titleText.AddClass("title");
        messageText.AddClass("message");
        actionButton.AddClass("action");
        closeButton.AddClass("close");

        AddClass("info");

        actionButton.Click += (_, _) => OnAction();
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        Rebuild();
    }

    /// <summary>Raised when the action button is pressed. The ToastHost dismisses the Toast afterwards.</summary>
    public event EventHandler? ActionInvoked;

    /// <summary>Raised when the close button is pressed. The ToastHost dismisses the Toast.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Raised after the ToastHost removed the Toast, for whatever reason.</summary>
    public event EventHandler? Closed;

    /// <summary>Gets or sets the message.</summary>
    public string Message
    {
        get => messageText.Text;
        set => messageText.Text = value ?? string.Empty;
    }

    /// <summary>Gets or sets the title. An empty title is not shown.</summary>
    public string Title
    {
        get => title;
        set
        {
            title = value ?? string.Empty;
            titleText.Text = title;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the icon. <see langword="null"/> shows none.</summary>
    public IImage? Icon
    {
        get => icon;
        set
        {
            icon = value;
            iconImage.Source = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the text of the action button. An empty text shows no button.</summary>
    public string ActionText
    {
        get => actionText;
        set
        {
            actionText = value ?? string.Empty;
            actionButton.Text = actionText;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the kind of the Toast, which sets its Class.</summary>
    public ToastSeverity Severity
    {
        get => severity;
        set
        {
            RemoveClass(ClassOf(severity));

            severity = value;

            AddClass(ClassOf(value));
        }
    }

    /// <summary>Gets or sets how long the Toast stays. <see langword="null"/> takes the <see cref="ToastHost.Duration"/> of the host; <see cref="TimeSpan.Zero"/> keeps it until it is dismissed.</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>Gets whether the pointer is over the Toast, which stops its timer.</summary>
    public bool IsHovered => PseudoStates.Contains("hover");

    /// <summary>Gets the time the Toast has left, or <see langword="null"/> when it stays until it is dismissed.</summary>
    internal TimeSpan? Remaining { get; set; }

    /// <summary>Gets the time of the last <see cref="ToastHost.Tick"/> that counted for this Toast, or <see langword="null"/> before the first.</summary>
    internal TimeSpan? Counted { get; set; }

    /// <summary>Raises <see cref="Closed"/>.</summary>
    internal void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>Gets the Class of a severity.</summary>
    /// <param name="value">The severity.</param>
    /// <returns>The lower case name.</returns>
    static string ClassOf(ToastSeverity value) => value.ToString().ToLowerInvariant();

    /// <summary>Raises <see cref="ActionInvoked"/> and then asks the host to dismiss the Toast.</summary>
    void OnAction()
    {
        ActionInvoked?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Puts the parts that are in use into the Toast, in order.</summary>
    void Rebuild()
    {
        Children.Clear();
        body.Children.Clear();

        if (title.Length > 0)
            body.Children.Add(titleText);

        body.Children.Add(messageText);

        if (icon is not null)
            Children.Add(iconImage);

        Children.Add(body);

        if (actionText.Length > 0)
            Children.Add(actionButton);

        Children.Add(closeButton);
    }
}

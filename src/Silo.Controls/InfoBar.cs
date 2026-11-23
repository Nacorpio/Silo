using Silo.Layout;
using Silo.Rendering;

namespace Silo;

/// <summary>An inline banner with a title, a message, an optional action button and an optional close button.</summary>
/// <remarks>
/// <para>
/// An InfoBar sits in the layout, not above it: it has no timer and is not an overlay. It has the Class of its
/// <see cref="Severity"/> (<c>info</c>, <c>success</c>, <c>warning</c> or <c>error</c>), and its parts the Classes
/// <c>body</c>, <c>title</c>, <c>message</c>, <c>action</c> and <c>close</c>.
/// </para>
/// <para>
/// The close button sets <see cref="IsOpen"/> to <see langword="false"/> and raises <see cref="Closed"/>. A closed InfoBar has
/// the Class <c>closed</c>, draws nothing and, in the Default theme, takes no space. Apply styles again after any change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var bar = new InfoBar { Title = "Update available", Message = "Restart to finish.", ActionText = "Restart", IsClosable = true };
///
/// bar.Severity = Severity.Warning;
/// bar.ActionInvoked += (_, _) => app.Restart();
/// </code>
/// </example>
public class InfoBar : Control
{
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


    /// <summary>The kind of the InfoBar.</summary>
    Severity severity = Severity.Info;

    /// <summary>Whether the close button is shown.</summary>
    bool isClosable;

    /// <summary>Whether the InfoBar is shown.</summary>
    bool isOpen = true;


    /// <summary>
    /// Runs before the first InfoBar is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static InfoBar()
    {
    }

    /// <summary>Creates an open InfoBar with no text.</summary>
    public InfoBar()
    {
        body.AddClass("body");
        titleText.AddClass("title");
        messageText.AddClass("message");
        actionButton.AddClass("action");
        closeButton.AddClass("close");

        AddClass("info");

        actionButton.Click += (_, _) => ActionInvoked?.Invoke(this, EventArgs.Empty);
        closeButton.Click += (_, _) => IsOpen = false;

        Rebuild();
    }


    /// <summary>Raised when the action button is pressed.</summary>
    public event EventHandler? ActionInvoked;

    /// <summary>Raised after the close button closed the InfoBar.</summary>
    public event EventHandler? Closed;


    /// <summary>Gets or sets the title. An empty title is not shown.</summary>
    public string Title
    {
        get => titleText.Text;
        set
        {
            titleText.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the message.</summary>
    public string Message
    {
        get => messageText.Text;
        set => messageText.Text = value ?? string.Empty;
    }

    /// <summary>Gets or sets the text of the action button. An empty text shows no button.</summary>
    public string ActionText
    {
        get => actionButton.Text;
        set
        {
            actionButton.Text = value ?? string.Empty;

            Rebuild();
        }
    }

    /// <summary>Gets or sets whether the InfoBar has a close button.</summary>
    public bool IsClosable
    {
        get => isClosable;
        set
        {
            isClosable = value;

            Rebuild();
        }
    }

    /// <summary>Gets or sets whether the InfoBar is shown. Raises <see cref="Closed"/> when it changes to <see langword="false"/>.</summary>
    public bool IsOpen
    {
        get => isOpen;
        set
        {
            if (isOpen == value)
                return;

            isOpen = value;

            if (value)
                RemoveClass("closed");
            else
                AddClass("closed");

            Rebuild();

            if (!value)
                Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the kind of the InfoBar, which sets its Class.</summary>
    public Severity Severity
    {
        get => severity;
        set
        {
            RemoveClass(ClassOf(severity));

            severity = value;

            AddClass(ClassOf(value));
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        if (isOpen)
            base.OnRender(context, bounds);
    }

    /// <summary>Gets the Class of a severity.</summary>
    /// <param name="value">The severity.</param>
    /// <returns>The lower case name.</returns>
    static string ClassOf(Severity value) => value.ToString().ToLowerInvariant();

    /// <summary>Puts the parts that are in use into the InfoBar, in order.</summary>
    void Rebuild()
    {
        Children.Clear();
        body.Children.Clear();

        if (!isOpen)
            return;

        if (titleText.Text.Length > 0)
            body.Children.Add(titleText);

        body.Children.Add(messageText);

        Children.Add(body);

        if (actionButton.Text.Length > 0)
            Children.Add(actionButton);

        if (isClosable)
            Children.Add(closeButton);
    }
}

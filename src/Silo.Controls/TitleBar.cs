using Silo.Rendering;
using Silo.Styling;

namespace Silo;

/// <summary>
/// The title bar of a window that draws its own frame: an icon, a title, room for your own Controls such as a menu, and the
/// minimize, maximize and close buttons.
/// </summary>
/// <remarks>
/// <para>
/// Put it at the top of the root <see cref="Window"/> and turn on <c>SdlWindow.CustomTitleBar</c>. The operating system still
/// moves and resizes the window: the empty parts of the bar, its icon and its title are drag areas
/// (<see cref="Control.IsWindowDragArea"/>), and a double press on them maximizes the window. Controls you put in
/// <see cref="Content"/> stay clickable. The buttons call <see cref="Window.Minimize"/>, <see cref="Window.Maximize"/> or
/// <see cref="Window.Restore"/> and <see cref="Window.Close"/> on the Window that contains the bar.
/// </para>
/// <para>
/// The buttons follow the platform by default: right on Windows and Linux, left on macOS. Style <c>button-position</c> to
/// change it. The close button gets the class <c>close</c>, so a theme can turn it red on hover.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var window = new Window { Title = "Notes" };
/// var bar = new TitleBar { Title = "Notes", Icon = appIcon, Content = new MenuBar() };
///
/// window.Children.Add(bar);
///
/// using var native = new SdlWindow(window) { CustomTitleBar = true };
/// </code>
/// </example>
public class TitleBar : Control
{
    /// <summary>Which side of the bar the three window buttons are on.</summary>
    public static readonly StyledProperty<ButtonPosition> ButtonPositionProperty =
        StyledProperty.Register<TitleBar, ButtonPosition>(
            "button-position",
            OperatingSystem.IsMacOS() ? ButtonPosition.Left : ButtonPosition.Right,
            ValueParsers.ButtonPlacement);


    /// <summary>The icon.</summary>
    readonly Image icon = new() { IsWindowDragArea = true };

    /// <summary>The title.</summary>
    readonly TextBlock title = new() { IsWindowDragArea = true };

    /// <summary>The Control that fills the middle.</summary>
    readonly Box middle = new() { IsWindowDragArea = true };

    /// <summary>The minimize button.</summary>
    readonly TitleBarButton minimize = new() { Glyph = "minus" };

    /// <summary>The maximize and restore button.</summary>
    readonly TitleBarButton maximize = new() { Glyph = "maximize" };

    /// <summary>The close button.</summary>
    readonly TitleBarButton close = new() { Glyph = "close" };

    /// <summary>The Control in the middle.</summary>
    Control? content;


    /// <summary>
    /// Runs before the first TitleBar is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static TitleBar()
    {
    }

    /// <summary>Creates a title bar.</summary>
    public TitleBar()
    {
        IsWindowDragArea = true;

        icon.AddClass("icon");
        title.AddClass("title");
        middle.AddClass("content");
        minimize.AddClass("minimize");
        maximize.AddClass("maximize");
        close.AddClass("close");

        minimize.Click += (_, _) => OwnerWindow?.Minimize();
        maximize.Click += (_, _) => ToggleMaximize();
        close.Click += (_, _) => OwnerWindow?.Close();

        Order();
    }

    /// <summary>Gets or sets the icon, or <see langword="null"/> for none.</summary>
    public IImage? Icon
    {
        get => icon.Source;
        set
        {
            icon.Source = value;

            Order();
        }
    }

    /// <summary>Gets or sets the title.</summary>
    public string Title
    {
        get => title.Text;
        set => title.Text = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the Control between the title and the buttons, such as a <see cref="MenuBar"/> or tabs. It is not a drag
    /// area, so it keeps handling the pointer, and it takes the room that is left.
    /// </summary>
    public Control? Content
    {
        get => content;
        set
        {
            content = value;

            middle.Children.Clear();

            if (value is not null)
                middle.Children.Add(value);
        }
    }

    /// <summary>Gets the minimize button.</summary>
    public Button MinimizeButton => minimize;

    /// <summary>Gets the maximize and restore button.</summary>
    public Button MaximizeButton => maximize;

    /// <summary>Gets the close button.</summary>
    public Button CloseButton => close;

    /// <summary>Gets the Window that contains the bar, or <see langword="null"/>.</summary>
    Window? OwnerWindow
    {
        get
        {
            for (var c = Parent as Control; c is not null; c = c.Parent as Control)
                if (c is Window window)
                    return window;

            return null;
        }
    }

    /// <inheritdoc/>
    protected override void OnStyled(StyleResolver resolver)
    {
        maximize.Glyph = OwnerWindow?.IsMaximized == true ? "restore" : "maximize";
        middle.IsWindowDragArea = content is null;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(StyledProperty property)
    {
        base.OnPropertyChanged(property);

        if (property == ButtonPositionProperty)
            Order();
    }

    /// <summary>Puts the children in order for the side that the buttons are on.</summary>
    void Order()
    {
        Children.Clear();

        var left = GetValue(ButtonPositionProperty) == ButtonPosition.Left;

        if (left)
            AddButtons();

        if (icon.Source is not null)
            Children.Add(icon);

        Children.Add(title);
        Children.Add(middle);

        if (!left)
            AddButtons();
    }

    /// <summary>Adds the three buttons in the order of the platform.</summary>
    void AddButtons()
    {
        var left = GetValue(ButtonPositionProperty) == ButtonPosition.Left;

        Control[] buttons = left ? [close, minimize, maximize] : [minimize, maximize, close];

        foreach (var button in buttons)
            Children.Add(button);
    }

    /// <summary>Maximizes the window, or restores it when it is maximized.</summary>
    void ToggleMaximize()
    {
        if (OwnerWindow is not { } window)
            return;

        if (window.IsMaximized)
            window.Restore();
        else
            window.Maximize();
    }
}

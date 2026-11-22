using Silo.Styling;

namespace Silo.Studio;

/// <summary>
/// The Studio window's contents: a title bar with the theme variant switch above a scrolling <see cref="ThemeMatrix"/> and,
/// when a theme is being edited, the <see cref="ThemeEditorPanel"/> beside it.
/// </summary>
/// <remarks>
/// The view does not choose Stylesheets. It raises <see cref="VariantChanged"/> when the user picks Light or Dark, and the
/// host applies the matching theme, so the same view works for a built-in theme and for a theme file that is being edited.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var view = new StudioView();
///
/// view.VariantChanged += (_, _) => window.Styles = new StyleResolver([.. DefaultTheme.Load(view.Variant), StudioStyles.Load()]);
/// </code>
/// </example>
public sealed class StudioView : Window
{
    /// <summary>The button that picks the light variant.</summary>
    readonly Button light = new() { Text = "Light", Name = "light" };

    /// <summary>The button that picks the dark variant.</summary>
    readonly Button dark = new() { Text = "Dark", Name = "dark" };

    /// <summary>The row that holds the gallery and the panels.</summary>
    readonly Box body = new() { Name = "body" };

    /// <summary>The variant that the matrix is shown in.</summary>
    ThemeVariant variant;


    /// <summary>Raised after <see cref="Variant"/> changed.</summary>
    public event EventHandler? VariantChanged;


    /// <summary>Creates the view with the light variant selected.</summary>
    /// <param name="editor">
    /// The theme that is being edited, or <see langword="null"/> to show the gallery only. A theme gets the editor panel beside
    /// the gallery.
    /// </param>
    public StudioView(ThemeEditorModel? editor = null)
    {
        Name = "studio";
        Title = "Silo Studio";
        InitialWidth = editor is null ? 820 : 1160;
        InitialHeight = 620;

        var bar = new Box { Name = "bar" };
        var title = new TextBlock { Text = "Theme gallery" };
        var scroll = new ScrollViewer { Name = "scroll", Content = Matrix };

        title.AddClass("title");

        light.Click += (_, _) => Variant = ThemeVariant.Light;
        dark.Click += (_, _) => Variant = ThemeVariant.Dark;

        bar.Children.Add(title);
        bar.Children.Add(light);
        bar.Children.Add(dark);

        body.Children.Add(scroll);

        if (editor is not null)
        {
            body.Children.Add(Editor = new ThemeEditorPanel(editor));

            Matrix.RowSelected += (_, row) => Editor.SelectType(row);
        }

        Children.Add(bar);
        Children.Add(body);

        ShowSelected();
    }

    /// <summary>Gets <c>Window</c>, so that the theme's Window Rules apply to Studio.</summary>
    public override string TypeName => "Window";

    /// <summary>Gets the theme editor beside the gallery, or <see langword="null"/> when no theme is being edited.</summary>
    public ThemeEditorPanel? Editor { get; }

    /// <summary>Gets the table of Controls in all of their states.</summary>
    public ThemeMatrix Matrix { get; } = new();

    /// <summary>Gets or sets the theme variant that the user has picked. The default is <see cref="ThemeVariant.Light"/>.</summary>
    public ThemeVariant Variant
    {
        get => variant;
        set
        {
            if (variant == value)
                return;

            variant = value;

            ShowSelected();

            VariantChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Adds a panel beside the gallery and the editor, such as the list of Previews.</summary>
    /// <param name="panel">The panel.</param>
    /// <remarks>The window grows by the width of the panel when it was not opened yet.</remarks>
    public void AddPanel(Control panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        body.Children.Add(panel);

        InitialWidth += 340;
    }

    /// <summary>Marks the button of the current variant with the <c>selected</c> Class.</summary>
    void ShowSelected()
    {
        SetSelected(light, variant == ThemeVariant.Light);
        SetSelected(dark, variant == ThemeVariant.Dark);
    }

    /// <summary>Adds or removes the <c>selected</c> Class.</summary>
    /// <param name="button">The button.</param>
    /// <param name="selected">Whether it is the picked one.</param>
    static void SetSelected(Button button, bool selected)
    {
        _ = selected ? button.AddClass("selected") : button.RemoveClass("selected");
    }
}

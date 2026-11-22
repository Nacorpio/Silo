using Silo.Styling;

namespace Silo.Studio;

/// <summary>
/// The Studio window's contents: a title bar with the theme variant switch above a scrolling <see cref="ThemeMatrix"/>.
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

    /// <summary>The variant that the matrix is shown in.</summary>
    ThemeVariant variant;


    /// <summary>Raised after <see cref="Variant"/> changed.</summary>
    public event EventHandler? VariantChanged;


    /// <summary>Creates the view with the light variant selected.</summary>
    public StudioView()
    {
        Name = "studio";
        Title = "Silo Studio";
        InitialWidth = 820;
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

        Children.Add(bar);
        Children.Add(scroll);

        ShowSelected();
    }

    /// <summary>Gets <c>Window</c>, so that the theme's Window Rules apply to Studio.</summary>
    public override string TypeName => "Window";

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

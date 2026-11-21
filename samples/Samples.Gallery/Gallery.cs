using Silo;
using Silo.Styling;

/// <summary>The controls and the Stylesheet that the gallery shows.</summary>
static class Gallery
{
    /// <summary>The gallery's own Stylesheet: layout and the button variants. It is applied after the default theme.</summary>
    const string Sheet = """
        {
          "rules": {
            "#root": {
              "background": "#f3f4f6", "padding": "16px", "gap": "12px", "flex-direction": "column",
              "align-items": "start"
            },
            ".title": { "font-size": "24px", "font-weight": "bold" },
            ".muted": { "color": "#6b7280" },
            "#row": { "gap": "8px", "flex-direction": "row" },
            "#options": { "gap": "6px", "flex-direction": "column", "align-items": "start" },
            "ProgressBar": { "width": "240px" },
            "Slider": { "width": "240px" },
            "TextBox": { "width": "240px" },
            "ListView": { "width": "240px", "height": "100px" },
            "Button.secondary": {
              "background": "#e5e7eb", "color": "#1f2937",
              "&:hover": { "background": "#d1d5db" },
              "&:active": { "background": "#9ca3af" }
            },
            "Button.danger": {
              "background": "#ef4444",
              "&:hover": { "background": "#dc2626" },
              "&:active": { "background": "#b91c1c" }
            }
          }
        }
        """;

    /// <summary>Parses the gallery's Stylesheet.</summary>
    /// <returns>The Stylesheet.</returns>
    public static Stylesheet Styles() => JsoncStylesheetReader.Read(Sheet, "gallery.jsonc");

    /// <summary>Builds the control tree.</summary>
    /// <returns>The root Control.</returns>
    public static Box Build()
    {
        var root = new Box { Name = "root" };
        var title = new TextBlock { Text = "Silo gallery" };
        var hint = new TextBlock { Text = "Edit theme.jsonc while this runs to restyle it." };
        var row = new Box { Name = "row" };
        var count = new TextBlock { Text = "Clicks: 0" };

        title.AddClass("title");
        hint.AddClass("muted");

        var primary = new Button { Text = "Click me" };
        var secondary = new Button { Text = "Secondary" };
        var danger = new Button { Text = "Reset" };

        secondary.AddClass("secondary");
        danger.AddClass("danger");

        var clicks = 0;

        primary.Click += (_, _) => count.Text = $"Clicks: {++clicks}";
        danger.Click += (_, _) => count.Text = $"Clicks: {clicks = 0}";

        row.Children.Add(primary);
        row.Children.Add(secondary);
        row.Children.Add(danger);

        var options = new Box { Name = "options" };
        var progress = new ProgressBar { Value = 0.6 };
        var small = new RadioButton { Text = "Small", Group = "size", IsChecked = true };
        var large = new RadioButton { Text = "Large", Group = "size" };

        options.Children.Add(new CheckBox { Text = "Remember me", IsChecked = true });
        options.Children.Add(small);
        options.Children.Add(large);
        options.Children.Add(new ToggleSwitch { Text = "Dark mode" });
        options.Children.Add(progress);

        var size = new ComboBox { Items = ["Small", "Medium", "Large", "Extra large"], SelectedIndex = 1 };

        options.Children.Add(size);

        var name = new TextBox { Placeholder = "Type your name" };
        var greeting = new TextBlock { Text = "Hello!" };

        name.TextChanged += (_, _) => greeting.Text = $"Hello, {(name.Text.Length == 0 ? "stranger" : name.Text)}!";
        options.Children.Add(name);
        options.Children.Add(greeting);

        var list = new ListView { Items = Enumerable.Range(1, 200).Select(n => $"Row {n}").ToList() };

        list.SelectedIndex = 1;
        options.Children.Add(list);

        var slider = new Slider { Value = 0.6 };

        slider.ValueChanged += (_, _) => progress.Value = slider.Value;
        options.Children.Add(slider);

        root.Children.Add(title);
        root.Children.Add(hint);
        root.Children.Add(row);
        root.Children.Add(count);
        root.Children.Add(options);

        return root;
    }
}

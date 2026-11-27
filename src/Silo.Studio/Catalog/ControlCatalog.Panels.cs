using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Panels category.</summary>
    static IEnumerable<CatalogEntry> Panels() =>
    [
        new("GroupBox", "Panels", () => new GroupBox { Header = "Options", Content = new TextBlock { Text = "Wrap lines" } }),
        new("Card", "Panels", CreateCard),
        new("ScrollBar", "Panels", CreateScrollBar),
        new("ScrollViewer", "Panels", CreateScrollViewer),
        new("AspectRatioBox", "Panels", CreateAspectRatioBox),
        new("Terminal", "Panels", CreateTerminal),
        new("Transform", "Panels", CreateTransforms),
        new("BusyIndicator", "Panels", CreateBusyIndicator),
        new("SplitPane", "Panels", CreateSplitPane),
        new("InfoBar", "Panels", CreateInfoBars),
    ];

    /// <summary>Creates a SplitPane with a narrow pane on the left and a wide one on the right.</summary>
    static SplitPane CreateSplitPane()
    {
        var split = new SplitPane();

        split.SetValue(Control.WidthProperty, new Silo.Styling.Values.Length(300, Silo.Styling.Values.LengthUnit.Px));
        split.SetValue(Control.HeightProperty, new Silo.Styling.Values.Length(70, Silo.Styling.Values.LengthUnit.Px));
        split.SetValue(Control.FlexShrinkProperty, 0);

        split.AddPane(new TextBlock { Text = "Files" }, minSize: 60, size: 100);
        split.AddPane(new TextBlock { Text = "Editor" }, minSize: 80);

        return split;
    }

    /// <summary>Creates a busy card: a Card dimmed by a BusyIndicator with a message.</summary>
    static BusyIndicator CreateBusyIndicator()
    {
        var busy = new BusyIndicator { Content = CreateCard(), Message = "Loading…", IsBusy = true };

        busy.SetValue(Control.WidthProperty, new Silo.Styling.Values.Length(220, Silo.Styling.Values.LengthUnit.Px));
        busy.SetValue(Control.HeightProperty, new Silo.Styling.Values.Length(130, Silo.Styling.Values.LengthUnit.Px));
        busy.SetValue(Control.FlexShrinkProperty, 0);

        return busy;
    }

    /// <summary>Creates a Terminal that shows a shell session with colors, styles and a cursor.</summary>
    static Terminal CreateTerminal()
    {
        const string Esc = "\u001b";

        var terminal = new Terminal(52, 11);

        terminal.Write($"{Esc}[1;32mnac@silo{Esc}[0m:{Esc}[1;34m~/silo{Esc}[0m$ ls --color\r\n");
        terminal.Write($"{Esc}[1;34msrc{Esc}[0m  {Esc}[1;34mtests{Esc}[0m  {Esc}[32mbuild.sh{Esc}[0m  README.md  {Esc}[31marchive.tar.gz{Esc}[0m\r\n");
        terminal.Write($"{Esc}[1;32mnac@silo{Esc}[0m:{Esc}[1;34m~/silo{Esc}[0m$ dotnet test\r\n");
        terminal.Write($"  {Esc}[32mPassed!{Esc}[0m  total: 874  {Esc}[2mfailed: 0{Esc}[0m\r\n");
        terminal.Write($"{Esc}[4munderline{Esc}[0m {Esc}[3mitalic{Esc}[0m {Esc}[9mstrike{Esc}[0m {Esc}[7mreverse{Esc}[0m {Esc}[1mbold{Esc}[0m\r\n");

        for (var i = 0; i < 8; i++)
            terminal.Write($"{Esc}[4{i}m  ");

        for (var i = 0; i < 8; i++)
            terminal.Write($"{Esc}[10{i}m  ");

        terminal.Write($"{Esc}[0m\r\n");

        for (var i = 0; i < 36; i++)
            terminal.Write($"{Esc}[48;2;{255 - (i * 7)};{i * 7};180m ");

        terminal.Write($"{Esc}[0m\r\n{Esc}[1;32mnac@silo{Esc}[0m:{Esc}[1;34m~/silo{Esc}[0m$ ");
        terminal.SetValue(Control.FlexShrinkProperty, 0);

        return terminal;
    }

    /// <summary>Creates a 16:9 Card letterboxed inside a square AspectRatioBox.</summary>
    static AspectRatioBox CreateAspectRatioBox()
    {
        var card = new Card();

        card.Children.Add(new TextBlock { Text = "16 : 9" });

        var box = new AspectRatioBox { Ratio = 16.0 / 9, Content = card };

        box.SetValue(Control.WidthProperty, new Silo.Styling.Values.Length(160, Silo.Styling.Values.LengthUnit.Px));
        box.SetValue(Control.HeightProperty, new Silo.Styling.Values.Length(160, Silo.Styling.Values.LengthUnit.Px));
        box.SetValue(Control.FlexShrinkProperty, 0);

        return box;
    }

    /// <summary>Creates a row of buttons that are moved, rotated and scaled with the <c>transform</c> property.</summary>
    static Box CreateTransforms()
    {
        var row = new Box();

        row.AddClass("badges");

        var transforms = new (string Text, Silo.Styling.Values.Transform Transform)[]
        {
            ("none", Silo.Styling.Values.Transform.None),
            ("rotate 12", new(0, 0, 12, 1, 1)),
            ("scale 1.25", new(0, 0, 0, 1.25, 1.25)),
            ("move 0 -6", new(0, -6, 0, 1, 1)),
            ("all", new(4, 4, -8, 1.15, 1.15)),
        };

        foreach (var (text, transform) in transforms)
        {
            var button = new Button { Text = text };

            button.SetValue(Control.TransformProperty, transform);
            button.SetValue(Control.FlexShrinkProperty, 0);
            row.Children.Add(button);
        }

        return row;
    }

    /// <summary>Creates a ScrollViewer with a tall list of lines, scrolled a little, with its bar always shown.</summary>
    static ScrollViewer CreateScrollViewer()
    {
        var lines = new Box();

        lines.SetValue(Control.FlexDirectionProperty, Silo.Layout.FlexDirection.Column);

        for (var i = 1; i <= 12; i++)
            lines.Children.Add(new TextBlock { Text = $"Line {i}" });

        var viewer = new ScrollViewer { Content = lines };

        viewer.SetValue(ScrollViewer.ScrollBarVisibilityProperty, "always");
        viewer.SetValue(Control.WidthProperty, new Silo.Styling.Values.Length(160, Silo.Styling.Values.LengthUnit.Px));
        viewer.SetValue(Control.HeightProperty, new Silo.Styling.Values.Length(100, Silo.Styling.Values.LengthUnit.Px));
        viewer.SetValue(Control.FlexShrinkProperty, 0);

        return viewer;
    }

    /// <summary>Creates a vertical ScrollBar scrolled a third of the way.</summary>
    static ScrollBar CreateScrollBar()
    {
        var bar = new ScrollBar { Maximum = 300, ViewportSize = 100, Value = 100 };

        bar.SetValue(Control.HeightProperty, new Silo.Styling.Values.Length(80, Silo.Styling.Values.LengthUnit.Px));

        return bar;
    }

    /// <summary>Creates a Card with a header, content and a footer.</summary>
    static Card CreateCard() => new()
    {
        Header = new TextBlock { Text = "Build 42" },
        Content = new TextBlock { Text = "Passed in 3 minutes" },
        Footer = new TextBlock { Text = "2 hours ago" },
    };

    /// <summary>Creates a column of one InfoBar of each severity.</summary>
    static Box CreateInfoBars()
    {
        var column = new Box();

        column.AddClass("infobars");

        column.Children.Add(new InfoBar { Title = "Update", Message = "Version 2 is ready", ActionText = "Restart", IsClosable = true });
        column.Children.Add(new InfoBar { Message = "Settings saved", Severity = Severity.Success });
        column.Children.Add(new InfoBar { Message = "Disk almost full", Severity = Severity.Warning, IsClosable = true });
        column.Children.Add(new InfoBar { Title = "Error", Message = "Could not connect", Severity = Severity.Error });

        return column;
    }
}

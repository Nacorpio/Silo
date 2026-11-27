using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Display category.</summary>
    static IEnumerable<CatalogEntry> Display() =>
    [
        new("Label", "Display", CreateLabels),
        new("Badge", "Display", CreateBadges),
        new("Avatar", "Display", CreateAvatars),
        new("EmptyState", "Display", CreateEmptyState),
        new("Skeleton", "Display", CreateSkeletons),
        new("StatList", "Display", CreateStatList),
        new("Icon", "Display", CreateIcons),
        new("HyperlinkButton", "Display", () => new HyperlinkButton { Text = "Read the docs" }),
    ];

    /// <summary>Creates a column of a plain Label and a required one.</summary>
    static Box CreateLabels()
    {
        var column = new Box();

        column.AddClass("stack");

        column.Children.Add(new Label { Text = "Name" });

        var required = new Label { Text = "Email" };

        required.AddClass("required");
        column.Children.Add(required);

        return column;
    }

    /// <summary>Creates a row of Badges: a count, a count above the maximum and one of each severity.</summary>
    static Box CreateBadges()
    {
        var row = new Box();

        row.AddClass("badges");

        row.Children.Add(new Badge { Value = 7 });
        row.Children.Add(new Badge { Value = 120 });
        row.Children.Add(new Badge { Text = "New", Severity = Severity.Info });
        row.Children.Add(new Badge { Text = "OK", Severity = Severity.Success });
        row.Children.Add(new Badge { Text = "Low", Severity = Severity.Warning });
        row.Children.Add(new Badge { Text = "Fail", Severity = Severity.Error });

        return row;
    }

    /// <summary>Creates a row of Avatars with initials, and one with a picture.</summary>
    static Box CreateAvatars()
    {
        var row = new Box();

        row.AddClass("badges");

        row.Children.Add(new Avatar { DisplayName = "Ada Lovelace" });
        row.Children.Add(new Avatar { DisplayName = "Grace Hopper" });
        row.Children.Add(new Avatar { DisplayName = "Linus" });
        row.Children.Add(new Avatar { DisplayName = "Alan Turing" });

        return row;
    }

    /// <summary>Creates an EmptyState with an icon, a title, a description and a button.</summary>
    static EmptyState CreateEmptyState()
    {
        var empty = new EmptyState
        {
            Glyph = "search",
            Title = "No results",
            Description = "Try another search term.",
            Action = new Button { Text = "Clear search" },
        };

        empty.SetValue(Control.FlexShrinkProperty, 0);

        return empty;
    }

    /// <summary>Creates a row of Skeletons: a circle, a line and a block.</summary>
    static Box CreateSkeletons()
    {
        var row = new Box();

        row.AddClass("badges");

        foreach (var shape in new[] { "skeleton-circle", "skeleton-line", "skeleton-block" })
        {
            var skeleton = new Skeleton();

            skeleton.AddClass(shape);
            skeleton.SetValue(Control.FlexShrinkProperty, 0);
            row.Children.Add(skeleton);
        }

        return row;
    }

    /// <summary>Creates a row with one Icon for each built-in glyph.</summary>
    static Box CreateIcons()
    {
        var row = new Box();

        row.AddClass("icons");

        foreach (var name in Glyphs.Names)
            row.Children.Add(new Icon { Glyph = name });

        return row;
    }

    /// <summary>Creates a StatList with the numbers of a repository.</summary>
    static StatList CreateStatList()
        => new()
        {
            Items =
            [
                new("activity", Label: "Activity"),
                new("star", "1", "star"),
                new("eye", "0", "watching"),
                new("fork", "0", "forks"),
            ],
        };
}

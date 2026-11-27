using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Navigation category.</summary>
    static IEnumerable<CatalogEntry> Navigation() =>
    [
        new("Breadcrumb", "Navigation", () => new Breadcrumb { Items = ["Home", "Projects", "Silo", "Readme"] }),
        new("Pager", "Navigation", () => new Pager { PageCount = 20, CurrentPage = 10 }),
        new("Accordion", "Navigation", CreateAccordion),
        new("Stepper", "Navigation", CreateStepper),
        new("Carousel", "Navigation", CreateCarousel),
    ];

    /// <summary>Creates an Accordion with three sections, the second open.</summary>
    static Accordion CreateAccordion()
    {
        var accordion = new Accordion();

        accordion.Add("General", new TextBlock { Text = "Name and language" });
        accordion.Add("Appearance", new TextBlock { Text = "Theme and font" });
        accordion.Add("Advanced", new TextBlock { Text = "Cache and logs" });

        accordion.ExpandedIndex = 1;

        return accordion;
    }

    /// <summary>Creates a Stepper at its second step.</summary>
    static Stepper CreateStepper()
        => new()
        {
            Steps = [new("Account", new TextBlock { Text = "Choose a name" }), new("Plan", new TextBlock { Text = "Pick a plan" }), new("Done", new TextBlock { Text = "All set" })],
            CurrentStep = 1,
        };

    /// <summary>Creates a Carousel of three texts at the second one.</summary>
    static Carousel CreateCarousel()
        => new() { Items = [new TextBlock { Text = "First slide" }, new TextBlock { Text = "Second slide" }, new TextBlock { Text = "Third slide" }], SelectedIndex = 1 };
}

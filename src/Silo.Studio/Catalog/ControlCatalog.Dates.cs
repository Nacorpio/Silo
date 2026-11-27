using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Dates category.</summary>
    static IEnumerable<CatalogEntry> Dates() =>
    [
        new("DatePicker", "Dates", () => new DatePicker { SelectedDate = new DateOnly(2026, 11, 23) }),
        new("Calendar", "Dates", CreateCalendar),
    ];

    /// <summary>Creates a Calendar that shows a fixed month with a selected date, so the gallery looks the same every day.</summary>
    static Calendar CreateCalendar()
    {
        var calendar = new Calendar
        {
            Culture = CultureInfo.InvariantCulture,
            FirstDayOfWeek = DayOfWeek.Monday,
            Today = new DateOnly(2026, 11, 23),
            DisplayedMonth = new DateOnly(2026, 11, 1),
            IsCursorVisible = false,
        };

        calendar.SelectedDate = new DateOnly(2026, 11, 12);

        return calendar;
    }
}

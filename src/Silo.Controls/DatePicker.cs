using System.Globalization;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// A field that shows a date and drops down a <see cref="Calendar"/> to pick another.
/// </summary>
/// <remarks>
/// <para>
/// Pressing the DatePicker opens a Calendar in the window's <see cref="OverlayLayer"/>, right below it. Pressing a day picks it
/// and closes the Calendar, and a press anywhere else or Escape closes it unchanged. With the keyboard focus, Enter opens it,
/// and then the arrows, PageUp, PageDown, Home, End and Enter work as in the Calendar. The field is read-only: the date cannot
/// be typed.
/// </para>
/// <para>
/// The date is shown with <see cref="Format"/>, which is the culture's short date pattern by default. While there is no date,
/// the <see cref="Placeholder"/> is shown in the <c>placeholder-color</c>. <see cref="Minimum"/>, <see cref="Maximum"/>,
/// <see cref="Culture"/> and <see cref="FirstDayOfWeek"/> are passed on to the Calendar.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var birthday = new DatePicker { Maximum = DateOnly.FromDateTime(DateTime.Today) };
///
/// birthday.SelectedDateChanged += (_, _) => profile.Birthday = birthday.SelectedDate;
/// </code>
/// </example>
public class DatePicker : Control, IPointerHandler
{
    /// <summary>The color of the placeholder text.</summary>
    public static readonly StyledProperty<Color> PlaceholderColorProperty =
        StyledProperty.Register<DatePicker, Color>("placeholder-color", new(156, 163, 175), Color.TryParse);


    /// <summary>Opens the Calendar below the DatePicker and tracks whether it is open.</summary>
    readonly AnchoredPopup popup;


    /// <summary>The calendar while it is open.</summary>
    Calendar? calendar;

    /// <summary>The picked date.</summary>
    DateOnly? selectedDate;

    /// <summary>The earliest date that can be picked.</summary>
    DateOnly? minimum;

    /// <summary>The latest date that can be picked.</summary>
    DateOnly? maximum;

    /// <summary>The culture of the format and of the Calendar.</summary>
    CultureInfo culture = CultureInfo.CurrentCulture;

    /// <summary>The first day of the week that was set, or <see langword="null"/> for the culture's.</summary>
    DayOfWeek? firstDayOfWeek;

    /// <summary>The format string of the date.</summary>
    string format = "d";


    /// <summary>
    /// Runs before the first DatePicker is created, so that the Styled properties above are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static DatePicker()
    {
    }

    /// <summary>Creates a DatePicker without a date.</summary>
    public DatePicker() => popup = new AnchoredPopup(this);

    /// <summary>Raised after <see cref="SelectedDate"/> changed.</summary>
    public event EventHandler? SelectedDateChanged;

    /// <summary>Gets or sets the picked date, or <see langword="null"/> for none.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The date is before <see cref="Minimum"/> or after <see cref="Maximum"/>.</exception>
    public DateOnly? SelectedDate
    {
        get => selectedDate;
        set
        {
            if (value is { } date && ((minimum is not null && date < minimum) || (maximum is not null && date > maximum)))
                throw new ArgumentOutOfRangeException(nameof(value), date, "The date is outside Minimum and Maximum.");

            if (value == selectedDate)
                return;

            selectedDate = value;

            SelectedDateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the earliest date that can be picked, or <see langword="null"/> for no limit. A picked date before it is cleared.</summary>
    public DateOnly? Minimum
    {
        get => minimum;
        set
        {
            minimum = value;

            Limit();
        }
    }

    /// <summary>Gets or sets the latest date that can be picked, or <see langword="null"/> for no limit. A picked date after it is cleared.</summary>
    public DateOnly? Maximum
    {
        get => maximum;
        set
        {
            maximum = value;

            Limit();
        }
    }

    /// <summary>Gets or sets the culture of the format and of the names in the Calendar. The default is the current culture.</summary>
    public CultureInfo Culture
    {
        get => culture;
        set => culture = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the first day of the week in the Calendar, or <see langword="null"/> for the culture's choice.</summary>
    public DayOfWeek? FirstDayOfWeek
    {
        get => firstDayOfWeek;
        set => firstDayOfWeek = value;
    }

    /// <summary>Gets or sets the format of the date: a standard or custom <see cref="DateOnly"/> format string. The default is <c>"d"</c>, the short date pattern.</summary>
    public string Format
    {
        get => format;
        set => format = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets or sets the text that is shown, dimmed, while no date is picked.</summary>
    public string Placeholder { get; set; } = "Select a date";

    /// <summary>Gets whether the Calendar is open.</summary>
    public bool IsOpen => popup.IsOpen;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>Gets the text that the field shows for the picked date.</summary>
    public string Text => selectedDate?.ToString(format, culture) ?? string.Empty;


    /// <summary>Opens the Calendar below the DatePicker.</summary>
    /// <returns><see langword="true"/> when it opened. It does not when it is open already or the DatePicker is not in a window.</returns>
    /// <remarks>Call it after <see cref="Control.Arrange"/>, since the Calendar sits where the DatePicker was arranged.</remarks>
    public bool Open()
    {
        if (IsOpen)
            return false;

        var view = new Calendar
        {
            Culture = culture,
            FirstDayOfWeek = firstDayOfWeek,
            Minimum = minimum,
            Maximum = maximum,
            SelectedDate = selectedDate,
            IsCursorVisible = true,
        };

        view.AddClass("popup");

        view.DatePicked += (_, _) => Choose();

        if (!popup.Open(view))
            return false;

        calendar = view;

        return true;
    }

    /// <summary>Closes the Calendar without changing the date.</summary>
    /// <returns><see langword="true"/> when it was open.</returns>
    public bool Close()
    {
        calendar = null;

        return popup.Close();
    }

    /// <summary>Opens the Calendar when it is closed. The pointer router calls this when the pointer goes down on the DatePicker.</summary>
    /// <returns><see langword="true"/> when a new frame is needed.</returns>
    /// <remarks>A press while the Calendar is open never gets here: the overlay layer closes the Calendar first.</remarks>
    public bool PointerDown(PointerEvent e) => Open();

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        if (IsOpen)
            return popup.KeyDown(key, KeyModifiers.None);

        return key == Key.Enter && Open();
    }

    /// <inheritdoc/>
    protected override Size MeasureContent(double? availableWidth)
    {
        var style = CurrentTextStyle();
        var measurer = Measurer();
        var sample = new DateOnly(2000, 12, 28).ToString(format, culture);
        var width = Math.Max(measurer.Measure(sample, style).Width, measurer.Measure(Placeholder, style).Width);

        return new Size(width + DropDownArrow.Space, measurer.Measure("Ag", style).Height);
    }

    /// <inheritdoc/>
    protected override void OnRender(IDrawingContext context, Rect bounds)
    {
        base.OnRender(context, bounds);

        var content = ContentBox(bounds);
        var style = CurrentTextStyle();
        var text = Text;
        var color = text.Length == 0 ? GetValue(PlaceholderColorProperty) : GetValue(ColorProperty);
        var y = content.Y + Math.Max(0, content.Height - Measurer().Measure("Ag", style).Height) / 2;

        context.Save();
        context.ClipRect(new Rect(content.X, content.Y, Math.Max(0, content.Width - DropDownArrow.Space), content.Height));
        context.DrawText(text.Length == 0 ? Placeholder : text, content.X, y, style, color);
        context.Restore();

        DropDownArrow.Draw(context, content, GetValue(ColorProperty));
    }

    /// <summary>Takes the date that was picked in the Calendar and closes it.</summary>
    void Choose()
    {
        var date = calendar?.SelectedDate;

        Close();

        SelectedDate = date;
    }

    /// <summary>Applies the limits after one of them changed.</summary>
    void Limit()
    {
        if (selectedDate is not { } date)
            return;

        if ((minimum is null || date >= minimum) && (maximum is null || date <= maximum))
            return;

        selectedDate = null;

        SelectedDateChanged?.Invoke(this, EventArgs.Empty);
    }
}

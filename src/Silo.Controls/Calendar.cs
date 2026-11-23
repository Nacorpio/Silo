using System.Globalization;

namespace Silo;

/// <summary>
/// A month grid in which the user picks one date.
/// </summary>
/// <remarks>
/// <para>
/// The Calendar shows the days of <see cref="DisplayedMonth"/> in weeks, with the days of the months next to it dimmed, and
/// arrow buttons that go to the previous and next month. Pressing a day picks it. Days before <see cref="Minimum"/> or after
/// <see cref="Maximum"/> are dimmed too and cannot be picked.
/// </para>
/// <para>
/// With the keyboard focus the arrows move the cursor by a day or a week, PageUp and PageDown by a month, Home and End to the
/// start and the end of the week, and Enter picks the cursor's day. The names and the first day of the week come from
/// <see cref="Culture"/>.
/// </para>
/// <para>
/// The look comes from the theme through Classes on the parts: <c>title</c>, <c>nav</c>, <c>weekday</c> and <c>day</c>, and on
/// a day <c>selected</c>, <c>today</c>, <c>cursor</c>, <c>outside</c> (another month) and <c>disabled</c>. Apply the styles again
/// after a change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var calendar = new Calendar { Minimum = new DateOnly(2026, 1, 1) };
///
/// calendar.SelectedDateChanged += (_, _) => booking.Start = calendar.SelectedDate;
/// </code>
/// </example>
public class Calendar : Control
{
    /// <summary>The number of weeks that are shown, which is enough for any month.</summary>
    const int Weeks = 6;

    /// <summary>The number of days in a week.</summary>
    const int Columns = 7;

    /// <summary>The button that goes to the previous month.</summary>
    readonly Button previous = new() { Text = "‹" };

    /// <summary>The button that goes to the next month.</summary>
    readonly Button next = new() { Text = "›" };

    /// <summary>The month and the year.</summary>
    readonly TextBlock title = new();

    /// <summary>The names of the days of the week.</summary>
    readonly TextBlock[] weekdays = new TextBlock[Columns];

    /// <summary>The day buttons, a week after another.</summary>
    readonly Button[] days = new Button[Weeks * Columns];

    /// <summary>The first day of the month that is shown.</summary>
    DateOnly displayedMonth;

    /// <summary>The picked date.</summary>
    DateOnly? selectedDate;

    /// <summary>The date that the keyboard moves and Enter picks.</summary>
    DateOnly cursor;

    /// <summary>Whether the cursor is marked. It is after the keyboard was used.</summary>
    bool cursorVisible;

    /// <summary>The earliest date that can be picked.</summary>
    DateOnly? minimum;

    /// <summary>The latest date that can be picked.</summary>
    DateOnly? maximum;

    /// <summary>The culture of the names.</summary>
    CultureInfo culture = CultureInfo.CurrentCulture;

    /// <summary>The first day of the week that was set, or <see langword="null"/> for the culture's.</summary>
    DayOfWeek? firstDayOfWeek;

    /// <summary>The date that is marked as today.</summary>
    DateOnly today = DateOnly.FromDateTime(DateTime.Today);


    /// <summary>Creates a Calendar that shows the current month.</summary>
    public Calendar()
    {
        var header = new Box();
        var weekRow = new Box();

        header.AddClass("header");
        previous.AddClass("nav");
        next.AddClass("nav");
        title.AddClass("title");
        weekRow.AddClass("weekdays");

        previous.Click += (_, _) => DisplayedMonth = displayedMonth.AddMonths(-1);
        next.Click += (_, _) => DisplayedMonth = displayedMonth.AddMonths(1);

        header.Children.Add(previous);
        header.Children.Add(title);
        header.Children.Add(next);

        for (var column = 0; column < Columns; column++)
        {
            weekdays[column] = new TextBlock();

            weekdays[column].AddClass("weekday");
            weekRow.Children.Add(weekdays[column]);
        }

        Children.Add(header);
        Children.Add(weekRow);

        for (var week = 0; week < Weeks; week++)
            Children.Add(BuildWeek(week));

        cursor = today;
        displayedMonth = new DateOnly(today.Year, today.Month, 1);

        Refresh();
    }

    /// <summary>Raised after <see cref="SelectedDate"/> changed.</summary>
    public event EventHandler? SelectedDateChanged;

    /// <summary>Raised when the user picks a date, by pressing a day or Enter, also when it is the date that was picked already.</summary>
    public event EventHandler? DatePicked;

    /// <summary>Gets or sets the picked date, or <see langword="null"/> for none.</summary>
    /// <remarks>Setting it shows its month and moves the cursor to it.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The date is before <see cref="Minimum"/> or after <see cref="Maximum"/>.</exception>
    public DateOnly? SelectedDate
    {
        get => selectedDate;
        set
        {
            if (value is { } date && !IsEnabled(date))
                throw new ArgumentOutOfRangeException(nameof(value), date, "The date is outside Minimum and Maximum.");

            if (value == selectedDate)
                return;

            selectedDate = value;

            if (value is { } shown)
            {
                cursor = shown;
                displayedMonth = new DateOnly(shown.Year, shown.Month, 1);
            }

            Refresh();

            SelectedDateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the month that is shown. Any date of the month does.</summary>
    public DateOnly DisplayedMonth
    {
        get => displayedMonth;
        set
        {
            displayedMonth = new DateOnly(value.Year, value.Month, 1);

            Refresh();
        }
    }

    /// <summary>Gets or sets the earliest date that can be picked, or <see langword="null"/> for no limit.</summary>
    /// <remarks>A picked date before it is cleared.</remarks>
    public DateOnly? Minimum
    {
        get => minimum;
        set
        {
            minimum = value;

            Limit();
        }
    }

    /// <summary>Gets or sets the latest date that can be picked, or <see langword="null"/> for no limit.</summary>
    /// <remarks>A picked date after it is cleared.</remarks>
    public DateOnly? Maximum
    {
        get => maximum;
        set
        {
            maximum = value;

            Limit();
        }
    }

    /// <summary>Gets or sets the culture of the month and day names. The default is the current culture.</summary>
    public CultureInfo Culture
    {
        get => culture;
        set
        {
            culture = value ?? throw new ArgumentNullException(nameof(value));

            Refresh();
        }
    }

    /// <summary>Gets or sets the day that a week starts with, or <see langword="null"/> for the culture's choice. The default is <see langword="null"/>.</summary>
    public DayOfWeek? FirstDayOfWeek
    {
        get => firstDayOfWeek;
        set
        {
            firstDayOfWeek = value;

            Refresh();
        }
    }

    /// <summary>Gets or sets the date that is marked as today. The default is the date when the Calendar was created.</summary>
    public DateOnly Today
    {
        get => today;
        set
        {
            today = value;

            Refresh();
        }
    }

    /// <summary>Gets or sets whether the cursor is marked. It is switched on by the keyboard and off by a press.</summary>
    public bool IsCursorVisible
    {
        get => cursorVisible;
        set
        {
            cursorVisible = value;

            Refresh();
        }
    }

    /// <summary>Gets the date that the keyboard moves and Enter picks.</summary>
    public DateOnly Cursor => cursor;

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        if (key == Key.Enter)
            return Pick(cursor);

        var offset = Offset(cursor);

        try
        {
            DateOnly? target = key switch
            {
                Key.Left => cursor.AddDays(-1),
                Key.Right => cursor.AddDays(1),
                Key.Up => cursor.AddDays(-Columns),
                Key.Down => cursor.AddDays(Columns),
                Key.PageUp => cursor.AddMonths(-1),
                Key.PageDown => cursor.AddMonths(1),
                Key.Home => cursor.AddDays(-offset),
                Key.End => cursor.AddDays(Columns - 1 - offset),

                _ => null,
            };

            return target is { } date && MoveCursor(date);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>Gets the position of a weekday in a week, from 0 for the first day of the week.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The position, from 0 to 6.</returns>
    int Offset(DateOnly date) => ((int)date.DayOfWeek - (int)EffectiveFirstDay + Columns) % Columns;

    /// <summary>Gets the day that a week starts with.</summary>
    DayOfWeek EffectiveFirstDay => firstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Tells whether a date can be picked.</summary>
    /// <param name="date">The date.</param>
    /// <returns><see langword="true"/> when it is within <see cref="Minimum"/> and <see cref="Maximum"/>.</returns>
    bool IsEnabled(DateOnly date) => (minimum is null || date >= minimum) && (maximum is null || date <= maximum);

    /// <summary>Builds the row of one week.</summary>
    /// <param name="week">The index of the week.</param>
    /// <returns>The row.</returns>
    Box BuildWeek(int week)
    {
        var row = new Box();

        row.AddClass("week");

        for (var column = 0; column < Columns; column++)
        {
            var index = (week * Columns) + column;
            var day = new Button();

            day.AddClass("day");

            day.Click += (_, _) => PickIndex(index);

            days[index] = day;

            row.Children.Add(day);
        }

        return row;
    }

    /// <summary>Gets the date of a day button.</summary>
    /// <param name="index">The index of the button.</param>
    /// <returns>The date.</returns>
    DateOnly DateAt(int index) => GridStart().AddDays(index);

    /// <summary>Finds the date of the first day button: the first day of the week that holds the first day of the month.</summary>
    /// <returns>The date.</returns>
    DateOnly GridStart() => displayedMonth.AddDays(-Offset(displayedMonth));

    /// <summary>Picks the day of a button.</summary>
    /// <param name="index">The index of the button.</param>
    void PickIndex(int index)
    {
        cursorVisible = false;

        Pick(DateAt(index));
    }

    /// <summary>Picks a date and raises <see cref="DatePicked"/>.</summary>
    /// <param name="date">The date.</param>
    /// <returns><see langword="true"/> when the date could be picked.</returns>
    bool Pick(DateOnly date)
    {
        if (!IsEnabled(date))
            return false;

        SelectedDate = date;

        Refresh();

        DatePicked?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>Moves the cursor, and shows its month.</summary>
    /// <param name="date">The new date.</param>
    /// <returns><see langword="true"/> when it moved. It does not move to a date that cannot be picked.</returns>
    bool MoveCursor(DateOnly date)
    {
        if (!IsEnabled(date))
            return false;

        cursor = date;
        cursorVisible = true;
        displayedMonth = new DateOnly(date.Year, date.Month, 1);

        Refresh();

        return true;
    }

    /// <summary>Applies the limits after one of them changed.</summary>
    void Limit()
    {
        if (selectedDate is { } date && !IsEnabled(date))
        {
            selectedDate = null;

            Refresh();

            SelectedDateChanged?.Invoke(this, EventArgs.Empty);

            return;
        }

        Refresh();
    }

    /// <summary>Writes the month, the names and the days into the parts.</summary>
    void Refresh()
    {
        var names = culture.DateTimeFormat.ShortestDayNames;
        var first = (int)EffectiveFirstDay;

        title.Text = displayedMonth.ToString("Y", culture);

        for (var column = 0; column < Columns; column++)
            weekdays[column].Text = names[(first + column) % Columns];

        for (var index = 0; index < days.Length; index++)
            Show(days[index], DateAt(index));
    }

    /// <summary>Shows a date on a day button.</summary>
    /// <param name="button">The button.</param>
    /// <param name="date">The date.</param>
    void Show(Button button, DateOnly date)
    {
        button.Text = date.Day.ToString(culture);

        SetClass(button, "selected", date == selectedDate);
        SetClass(button, "today", date == today);
        SetClass(button, "cursor", cursorVisible && date == cursor);
        SetClass(button, "outside", date.Month != displayedMonth.Month || date.Year != displayedMonth.Year);
        SetClass(button, "disabled", !IsEnabled(date));
    }

    /// <summary>Adds or removes a Class.</summary>
    /// <param name="element">The Element.</param>
    /// <param name="name">The Class.</param>
    /// <param name="on"><see langword="true"/> to add it.</param>
    static void SetClass(Element element, string name, bool on)
    {
        if (on)
            element.AddClass(name);
        else
            element.RemoveClass(name);
    }
}

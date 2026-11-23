using System.Globalization;
using Calendar = Silo.Calendar;
using Silo;
using Xunit;

public class CalendarTests
{
    static readonly DateOnly Today = new(2026, 11, 23);

    static Calendar Create() => new()
    {
        Culture = CultureInfo.InvariantCulture,
        FirstDayOfWeek = DayOfWeek.Monday,
        Today = Today,
        DisplayedMonth = Today,
    };

    [Fact]
    public void It_shows_the_month_of_today_with_the_cursor_on_today()
    {
        var calendar = Create();

        Assert.Equal(new DateOnly(2026, 11, 1), calendar.DisplayedMonth);
        Assert.Equal(Today, calendar.Cursor);
        Assert.Null(calendar.SelectedDate);
    }

    [Fact]
    public void Selecting_a_date_raises_the_event_and_shows_its_month()
    {
        var calendar = Create();
        var raised = 0;

        calendar.SelectedDateChanged += (_, _) => raised++;

        calendar.SelectedDate = new DateOnly(2027, 2, 14);

        Assert.Equal(1, raised);
        Assert.Equal(new DateOnly(2027, 2, 1), calendar.DisplayedMonth);

        calendar.SelectedDate = new DateOnly(2027, 2, 14);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void A_date_outside_the_limits_is_refused()
    {
        var calendar = Create();

        calendar.Minimum = new DateOnly(2026, 11, 10);
        calendar.Maximum = new DateOnly(2026, 11, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SelectedDate = new DateOnly(2026, 11, 21));
    }

    [Fact]
    public void Tightening_the_limits_clears_a_selection_that_no_longer_fits()
    {
        var calendar = Create();

        calendar.SelectedDate = new DateOnly(2026, 11, 5);
        calendar.Minimum = new DateOnly(2026, 11, 10);

        Assert.Null(calendar.SelectedDate);
    }

    [Fact]
    public void The_arrows_move_the_cursor_by_a_day_or_a_week()
    {
        var calendar = Create();

        calendar.KeyDown(Key.Right);
        Assert.Equal(new DateOnly(2026, 11, 24), calendar.Cursor);

        calendar.KeyDown(Key.Down);
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.Cursor);
        Assert.Equal(new DateOnly(2026, 12, 1), calendar.DisplayedMonth);

        calendar.KeyDown(Key.Left);
        calendar.KeyDown(Key.Up);
        Assert.Equal(new DateOnly(2026, 11, 23), calendar.Cursor);
    }

    [Fact]
    public void Page_keys_move_by_a_month_and_home_and_end_go_to_the_week_edges()
    {
        var calendar = Create();

        calendar.KeyDown(Key.PageUp);
        Assert.Equal(new DateOnly(2026, 10, 23), calendar.Cursor);

        calendar.KeyDown(Key.Home);
        Assert.Equal(new DateOnly(2026, 10, 19), calendar.Cursor);

        calendar.KeyDown(Key.End);
        Assert.Equal(new DateOnly(2026, 10, 25), calendar.Cursor);

        calendar.KeyDown(Key.PageDown);
        Assert.Equal(new DateOnly(2026, 11, 25), calendar.Cursor);
    }

    [Fact]
    public void Enter_picks_the_cursor_and_raises_date_picked_even_for_the_same_date()
    {
        var calendar = Create();
        var picked = 0;

        calendar.DatePicked += (_, _) => picked++;

        Assert.True(calendar.KeyDown(Key.Enter));
        Assert.True(calendar.KeyDown(Key.Enter));

        Assert.Equal(Today, calendar.SelectedDate);
        Assert.Equal(2, picked);
    }

    [Fact]
    public void The_cursor_does_not_move_onto_a_date_outside_the_limits()
    {
        var calendar = Create();

        calendar.Maximum = Today;

        calendar.KeyDown(Key.Right);

        Assert.Equal(Today, calendar.Cursor);
    }

    [Fact]
    public void The_first_day_of_the_week_follows_the_culture_unless_it_is_set()
    {
        var calendar = Create();

        calendar.Culture = new CultureInfo("en-US");
        calendar.FirstDayOfWeek = null;

        calendar.KeyDown(Key.Home);

        Assert.Equal(new DateOnly(2026, 11, 22), calendar.Cursor);
    }
}

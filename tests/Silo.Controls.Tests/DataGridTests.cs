using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class DataGridTests
{
    const string Sheet = """
        {
          "rules": {
            "DataGrid": { "width": "300px", "height": "130px", "row-height": "20px", "header-height": "20px", "alternate-row-color": "#eeeeee" }
          }
        }
        """;

    sealed record Person(string Name, int Age);

    sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    static readonly Person[] People =
    [
        new("Cara", 41), new("Abe", 30), new("Bo", 25), new("Dee", 52), new("Eli", 19), new("Fay", 33), new("Gus", 60),
    ];

    static (Box Root, DataGrid Grid) Setup(FakeTime? time = null)
    {
        var grid = new DataGrid { Rows = People, TimeProvider = time ?? new FakeTime() };

        grid.Columns.Add(new DataGridColumn("Name", row => ((Person)row).Name) { Width = 100 });
        grid.Columns.Add(new DataGridColumn("Age", row => ((Person)row).Age) { Star = 1, Alignment = TextAlign.Right });

        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        root.Children.Add(grid);
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        return (root, grid);
    }

    static List<string> Names(DataGrid grid)
        => Enumerable.Range(0, grid.Rows.Count).Select(i => ((Person)grid.Rows[i]).Name).ToList();

    static IReadOnlyList<string> Shown(DataGrid grid)
    {
        var shown = new List<string>();

        for (var i = 0; i < grid.Rows.Count; i++)
        {
            grid.SelectedIndex = i;

            shown.Add(((Person)grid.SelectedRow!).Name);
        }

        return shown;
    }

    [Fact]
    public void The_fixed_column_keeps_its_width_and_the_star_column_takes_the_rest()
    {
        var (_, grid) = Setup();

        Assert.Equal([100.0, 200.0], grid.ColumnWidths());
    }

    [Fact]
    public void A_star_column_is_never_narrower_than_its_minimum()
    {
        var (_, grid) = Setup();

        grid.Columns[0].Width = 290;
        grid.Columns[1].MinWidth = 60;

        Assert.Equal([290.0, 60.0], grid.ColumnWidths());
        Assert.Equal(50, grid.MaxHorizontalOffset);
    }

    [Fact]
    public void Cells_are_formatted_with_the_format_and_null_is_empty()
    {
        var column = new DataGridColumn("Size", row => row is double ? row : null) { Format = "N1" };

        Assert.Equal("12.5", column.TextOf(12.5));
        Assert.Equal(string.Empty, column.TextOf("x"));
    }

    [Fact]
    public void Pressing_a_header_sorts_ascending_then_descending_then_not_at_all()
    {
        var (_, grid) = Setup();
        var raised = 0;

        grid.SortChanged += (_, _) => raised++;

        Assert.True(grid.PointerDown(new(30, 10)));
        Assert.Equal(["Abe", "Bo", "Cara", "Dee", "Eli", "Fay", "Gus"], Shown(grid));

        grid.PointerDown(new(30, 10));
        Assert.True(grid.SortDescending);
        Assert.Equal(["Gus", "Fay", "Eli", "Dee", "Cara", "Bo", "Abe"], Shown(grid));

        grid.PointerDown(new(30, 10));
        Assert.Null(grid.SortColumn);
        Assert.Equal(["Cara", "Abe", "Bo", "Dee", "Eli", "Fay", "Gus"], Shown(grid));
        Assert.Equal(3, raised);
    }

    [Fact]
    public void Numbers_are_sorted_by_value_and_a_column_comparer_wins()
    {
        var (_, grid) = Setup();

        grid.SortBy(grid.Columns[1]);

        grid.SelectedIndex = 0;

        Assert.Equal(19, ((Person)grid.SelectedRow!).Age);

        grid.Columns[1].Comparer = Comparer<object?>.Create((a, b) => Comparer<int>.Default.Compare((int)b!, (int)a!));
        grid.SortBy(grid.Columns[1]);
        grid.SelectedIndex = 0;

        Assert.Equal(60, ((Person)grid.SelectedRow!).Age);
    }

    [Fact]
    public void Sorting_keeps_the_same_row_selected()
    {
        var (_, grid) = Setup();

        grid.SelectedIndex = 0;

        Assert.Equal("Cara", ((Person)grid.SelectedRow!).Name);

        grid.SortBy(grid.Columns[0]);

        Assert.Equal(2, grid.SelectedIndex);
        Assert.Equal("Cara", ((Person)grid.SelectedRow!).Name);
    }

    [Fact]
    public void Sorting_by_a_column_of_another_grid_is_refused()
    {
        var (_, grid) = Setup();

        Assert.Throws<ArgumentException>(() => grid.SortBy(new DataGridColumn("x", row => row)));
    }

    [Fact]
    public void Pressing_a_row_selects_it_and_a_second_press_activates_it()
    {
        var time = new FakeTime();
        var (_, grid) = Setup(time);
        var activated = new List<int>();

        grid.RowActivated += (_, index) => activated.Add(index);

        Assert.True(grid.PointerDown(new(10, 20 + 20 + 5)));
        Assert.Equal(1, grid.SelectedIndex);
        Assert.Empty(activated);

        time.Now = time.Now.AddMilliseconds(200);

        grid.PointerDown(new(10, 20 + 20 + 5));

        Assert.Equal([1], activated);
    }

    [Fact]
    public void A_slow_second_press_does_not_activate()
    {
        var time = new FakeTime();
        var (_, grid) = Setup(time);
        var activated = 0;

        grid.RowActivated += (_, _) => activated++;

        grid.PointerDown(new(10, 30));

        time.Now = time.Now.AddMilliseconds(900);

        grid.PointerDown(new(10, 30));

        Assert.Equal(0, activated);
    }

    [Fact]
    public void The_keys_move_the_selection_and_scroll_it_into_view()
    {
        var (_, grid) = Setup();

        grid.KeyDown(Key.Down);
        Assert.Equal(0, grid.SelectedIndex);

        grid.KeyDown(Key.End);
        Assert.Equal(6, grid.SelectedIndex);
        Assert.True(grid.Offset > 0);

        grid.KeyDown(Key.PageUp);
        Assert.Equal(1, grid.SelectedIndex);

        grid.KeyDown(Key.Home);
        Assert.Equal(0, grid.Offset);
    }

    [Fact]
    public void Enter_activates_the_selected_row()
    {
        var (_, grid) = Setup();
        var activated = new List<int>();

        grid.RowActivated += (_, index) => activated.Add(index);

        grid.KeyDown(Key.Enter);
        grid.SelectedIndex = 3;
        grid.KeyDown(Key.Enter);

        Assert.Equal([3], activated);
    }

    [Fact]
    public void Dragging_the_edge_of_a_header_resizes_the_column_and_makes_it_fixed()
    {
        var (_, grid) = Setup();

        grid.PointerDown(new(100, 10));

        Assert.True(grid.PointerDrag(new(140, 10)));
        Assert.Equal(140, grid.Columns[0].Width);

        grid.PointerDrag(new(-50, 10));

        Assert.Equal(grid.Columns[0].MinWidth, grid.Columns[0].Width);

        grid.PointerUp(default);

        Assert.False(grid.PointerDrag(new(200, 10)));

        grid.PointerDown(new(300, 10));
        grid.PointerDrag(new(250, 10));

        Assert.Equal(0, grid.Columns[1].Star);
        Assert.Equal(210, grid.Columns[1].Width);
    }

    [Fact]
    public void Left_and_right_scroll_sideways_only_when_the_columns_are_wider_than_the_grid()
    {
        var (_, grid) = Setup();

        Assert.False(grid.KeyDown(Key.Right));

        grid.Columns[0].Width = 500;

        Assert.True(grid.KeyDown(Key.Right));
        Assert.Equal(40, grid.HorizontalOffset);
    }

    [Fact]
    public void The_wheel_scrolls_the_rows_and_stops_at_the_end()
    {
        var (_, grid) = Setup();

        Assert.True(grid.ScrollBy(25));
        Assert.Equal(25, grid.Offset);

        grid.ScrollBy(1000);

        Assert.Equal(grid.MaxOffset, grid.Offset);
        Assert.False(grid.ScrollBy(1000));
    }

    [Fact]
    public void Only_the_rows_in_view_are_painted()
    {
        var (root, _) = Setup();
        var builder = new DisplayListBuilder();

        root.Render(builder);

        var texts = builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text).ToList();

        Assert.Contains("Cara", texts);
        Assert.DoesNotContain("Gus", texts);
    }

    [Fact]
    public void Setting_the_rows_clears_the_selection_and_keeps_the_sort()
    {
        var (_, grid) = Setup();

        grid.SortBy(grid.Columns[0]);
        grid.SelectedIndex = 2;

        grid.Rows = People[..3];

        Assert.Equal(-1, grid.SelectedIndex);
        Assert.Equal(grid.Columns[0], grid.SortColumn);
    }

    [Fact]
    public void The_pointer_router_highlights_the_row_under_the_pointer_and_clears_it_on_leave()
    {
        var (root, grid) = Setup();
        var router = new PointerRouter(root);

        Assert.True(router.Move(10, 20 + 5));
        Assert.True(router.Leave());
        Assert.False(grid.PointerLeave());
    }

    [Fact]
    public void A_header_icon_is_drawn_in_front_of_the_header_text()
    {
        var (root, grid) = Setup();
        var icon = new FakeImage(8, 8);

        grid.Columns[0].Icon = icon;

        var commands = Frame(root);
        var image = commands.OfType<DrawImageCommand>().Single();
        var header = commands.OfType<DrawTextCommand>().Single(c => c.Text == "Name");

        Assert.Equal(icon, image.Image);
        Assert.Equal(16, image.Destination.Width);
        Assert.Equal(image.Destination.X + 16 + 6, header.X);
    }

    [Fact]
    public void A_cell_icon_comes_from_the_binding_and_a_mask_takes_the_text_color()
    {
        var (root, grid) = Setup();
        var mask = new FakeMask(8, 8);

        grid.Columns[0].IconBinding = row => ((Person)row).Age > 40 ? mask : null;
        grid.SelectedIndex = 0;

        var commands = Frame(root);
        var drawn = commands.OfType<DrawMaskCommand>().ToList();

        Assert.Equal(2, drawn.Count);
        Assert.Equal(new Silo.Styling.Values.Color(255, 255, 255), drawn[0].Color);
        Assert.NotEqual(drawn[0].Color, drawn[1].Color);
    }

    static List<DrawCommand> Frame(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands];
    }
}

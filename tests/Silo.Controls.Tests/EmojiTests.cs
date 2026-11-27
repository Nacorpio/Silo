using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class EmojiTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "align-items": "start" },
            "EmojiPicker": { "padding": "4px" },
            "EmojiPanel": { "flex-direction": "column", "width": "300px" },
            "EmojiPanel .bar": { "flex-direction": "row" },
            "EmojiPanel .categories": { "flex-direction": "row" },
            "EmojiGrid": { "width": "300px", "height": "200px" },
            "SearchBox": { "width": "100px" }
          }
        }
        """;

    static (WindowScene Scene, Box Root) Scene(params Control[] content)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        foreach (var control in content)
            root.Children.Add(control);

        var scene = new WindowScene(root, new ManualAnimationClock()) { Styles = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]) };

        scene.Render(new Size(400, 500));

        return (scene, root);
    }

    static EmojiGrid ShownGrid(out Box root)
    {
        var grid = new EmojiGrid();

        (_, root) = Scene(grid);

        return grid;
    }

    [Fact]
    public void The_grid_has_a_section_for_each_group_and_a_Recent_section_first_when_there_are_recent_emoji()
    {
        var grid = ShownGrid(out _);

        Assert.Equal(EmojiCatalog.Groups, grid.SectionTitles);

        grid.Recent = ["\U0001F44D"];

        Assert.Equal("Recent", grid.SectionTitles[0]);
        Assert.Equal(EmojiCatalog.Groups.Count + 1, grid.SectionTitles.Count);
        Assert.Equal(EmojiCatalog.All.Count + 1, grid.Count);
    }

    [Fact]
    public void Pressing_a_cell_picks_its_emoji_and_a_title_or_the_gap_after_the_last_cell_picks_nothing()
    {
        var grid = ShownGrid(out _);
        var picked = new List<string>();

        grid.EmojiPicked += (_, emoji) => picked.Add(emoji);

        Assert.False(grid.PointerDown(new PointerEvent(10, 5)));
        Assert.True(grid.PointerDown(new PointerEvent(10, 30)));
        Assert.True(grid.PointerDown(new PointerEvent(10 + 32, 30)));

        Assert.Equal([EmojiCatalog.All[0].Value, EmojiCatalog.All[1].Value], picked);
    }

    [Fact]
    public void A_query_shows_the_matches_in_one_list_with_the_first_under_the_keyboard_and_Enter_picks_it()
    {
        var grid = ShownGrid(out _);
        string? picked = null;

        grid.EmojiPicked += (_, emoji) => picked = emoji;
        grid.Query = "grin";

        Assert.Empty(grid.SectionTitles);
        Assert.Equal(EmojiCatalog.Find("grin").Count, grid.Count);
        Assert.True(grid.KeyDown(Key.Enter));
        Assert.Equal(EmojiCatalog.Find("grin")[0].Value, picked);

        grid.Query = string.Empty;

        Assert.Equal(EmojiCatalog.Groups.Count, grid.SectionTitles.Count);
    }

    [Fact]
    public void The_arrows_move_by_a_cell_or_a_row_and_Home_and_End_go_to_the_ends()
    {
        var grid = ShownGrid(out _);
        var columns = (int)Math.Floor((300 - 10) / 32.0);

        grid.KeyDown(Key.Right);

        Assert.Equal(EmojiCatalog.All[0].Value, grid.CursorEmoji);

        grid.KeyDown(Key.Right);

        Assert.Equal(EmojiCatalog.All[1].Value, grid.CursorEmoji);

        grid.KeyDown(Key.Down);

        Assert.Equal(EmojiCatalog.All[1 + columns].Value, grid.CursorEmoji);

        grid.KeyDown(Key.Up);

        Assert.Equal(EmojiCatalog.All[1].Value, grid.CursorEmoji);

        grid.KeyDown(Key.End);

        Assert.Equal(EmojiCatalog.All[^1].Value, grid.CursorEmoji);
        Assert.True(grid.Offset > 0);

        grid.KeyDown(Key.Home);

        Assert.Equal(EmojiCatalog.All[0].Value, grid.CursorEmoji);
        Assert.Equal(0, grid.Offset);
    }

    [Fact]
    public void The_skin_tone_applies_to_the_emoji_that_have_tones_only()
    {
        var grid = ShownGrid(out _);
        var picked = new List<string>();

        grid.EmojiPicked += (_, emoji) => picked.Add(emoji);
        grid.SkinTone = SkinTone.Dark;
        grid.Query = "waving hand";
        grid.KeyDown(Key.Enter);
        grid.Query = "grinning face";
        grid.KeyDown(Key.Enter);

        Assert.Equal(["\U0001F44B\U0001F3FF", "\U0001F600"], picked);
    }

    [Fact]
    public void Scrolling_to_a_section_puts_its_title_at_the_top_and_makes_it_the_current_section()
    {
        var grid = ShownGrid(out var root);
        var changes = 0;

        grid.CurrentSectionChanged += (_, _) => changes++;

        Assert.True(grid.ScrollToSection(3));
        Assert.True(grid.Offset > 0);

        root.Render(new DisplayListBuilder());

        Assert.Equal(3, grid.CurrentSection);
        Assert.Equal(1, changes);
        Assert.False(grid.ScrollToSection(99));
    }

    [Fact]
    public void The_wheel_scrolls_and_stops_at_the_end()
    {
        var grid = ShownGrid(out _);

        Assert.True(grid.ScrollBy(100));
        Assert.Equal(100, grid.Offset);
        Assert.True(grid.ScrollBy(-1000));
        Assert.Equal(0, grid.Offset);
        Assert.False(grid.ScrollBy(-1));
    }

    [Fact]
    public void The_grid_draws_emoji_in_the_emoji_font_and_only_the_rows_in_view()
    {
        var grid = ShownGrid(out var root);
        var builder = new DisplayListBuilder();

        root.Render(builder);

        var texts = builder.Build().Commands.OfType<DrawTextCommand>().Where(c => c.Style.FontFamily == EmojiFonts.Default).ToList();

        Assert.NotEmpty(texts);
        Assert.True(texts.Count < 150);
        Assert.Equal(EmojiCatalog.All[0].Value, texts[0].Text);
    }

    [Fact]
    public void Typing_in_the_panel_searches_and_the_arrows_and_Enter_work_on_the_grid()
    {
        var panel = new EmojiPanel { CloseOnPick = false };

        Scene(panel);

        var picked = new List<string>();

        panel.EmojiPicked += (_, emoji) => picked.Add(emoji);
        panel.BeginKeyboard();

        Assert.True(panel.TextInput("grin"));
        Assert.Equal("grin", panel.Query);
        Assert.Equal(EmojiCatalog.Find("grin").Count, panel.Grid.Count);

        panel.KeyDown(Key.Right);
        panel.KeyDown(Key.Enter);

        Assert.Equal([EmojiCatalog.Find("grin")[1].Value], picked);
    }

    [Fact]
    public void A_pick_goes_first_in_the_recent_list_once_and_the_list_is_cut_at_the_maximum()
    {
        var panel = new EmojiPanel { CloseOnPick = false };
        var changes = 0;

        Scene(panel);

        panel.RecentChanged += (_, _) => changes++;

        for (var i = 0; i < EmojiPanel.MaxRecent + 5; i++)
        {
            panel.Query = EmojiCatalog.All[i].Name;
            panel.Grid.KeyDown(Key.Enter);
        }

        panel.Query = EmojiCatalog.All[10].Name;
        panel.Grid.KeyDown(Key.Enter);

        Assert.Equal(EmojiPanel.MaxRecent, panel.Recent.Count);
        Assert.Equal(EmojiCatalog.All[10].Value, panel.Recent[0]);
        Assert.Equal(1, panel.Recent.Count(e => e == EmojiCatalog.All[10].Value));
        Assert.Equal(EmojiPanel.MaxRecent + 6, changes);
    }

    [Fact]
    public void The_tone_button_steps_through_the_tones_and_back_to_the_default()
    {
        var panel = new EmojiPanel();
        var changes = 0;

        Scene(panel);

        panel.SkinToneChanged += (_, _) => changes++;

        var tone = panel.Children.OfType<Box>().First().Children.OfType<Button>().Single();

        for (var i = 1; i <= 6; i++)
        {
            tone.PointerDown();
            tone.PointerUp(inside: true);

            Assert.Equal((SkinTone)(i % 6), panel.SkinTone);
        }

        Assert.Equal(6, changes);
    }

    [Fact]
    public void A_pick_raises_Submitted_only_while_CloseOnPick_is_on()
    {
        var panel = new EmojiPanel();
        var submitted = 0;

        Scene(panel);

        panel.Submitted += (_, _) => submitted++;
        panel.Query = "grin";
        panel.Grid.KeyDown(Key.Enter);

        Assert.Equal(1, submitted);

        panel.CloseOnPick = false;
        panel.Grid.KeyDown(Key.Enter);

        Assert.Equal(1, submitted);
    }

    [Fact]
    public void The_picker_opens_the_panel_and_a_pick_sets_the_emoji_and_closes_it()
    {
        var picker = new EmojiPicker();
        var (scene, _) = Scene(picker);
        var picked = new List<string>();

        picker.EmojiPicked += (_, emoji) => picked.Add(emoji);

        Assert.True(picker.Open());
        Assert.True(picker.IsOpen);
        Assert.False(picker.Open());
        Assert.Equal(1, scene.Overlays.Count);

        picker.TextInput("thumbs up");
        picker.KeyDown(Key.Enter);

        Assert.False(picker.IsOpen);
        Assert.Equal(EmojiCatalog.Find("thumbs up")[0].Value, picker.SelectedEmoji);
        Assert.Equal([picker.SelectedEmoji!], picked);
        Assert.Equal([picker.SelectedEmoji!], picker.Recent);
    }

    [Fact]
    public void The_picker_remembers_the_recent_emoji_and_the_tone_for_the_next_time_it_opens()
    {
        var picker = new EmojiPicker { Recent = ["\U0001F602"], SkinTone = SkinTone.Medium };
        var (scene, _) = Scene(picker);

        picker.Open();

        var commands = scene.Render(new Size(400, 500))!.Commands.OfType<DrawTextCommand>().ToList();

        Assert.Contains(commands, c => c.Text == "Recent");
        Assert.Contains(commands, c => c.Text == "\U0001F602");
        Assert.Contains(commands, c => c.Text == "\U0001F44B\U0001F3FD");
    }

    [Fact]
    public void An_empty_picker_shows_its_placeholder_and_a_picked_one_the_emoji_in_the_emoji_font()
    {
        var picker = new EmojiPicker { Placeholder = "React" };
        var (_, root) = Scene(picker);

        var empty = new DisplayListBuilder();

        root.Render(empty);

        Assert.Contains(empty.Build().Commands, c => c is DrawTextCommand { Text: "React" });

        picker.SelectedEmoji = "\U0001F44D";

        var filled = new DisplayListBuilder();

        root.Render(filled);

        Assert.Contains(filled.Build().Commands, c => c is DrawTextCommand { Text: "\U0001F44D" } t && t.Style.FontFamily == EmojiFonts.Default);
    }
}

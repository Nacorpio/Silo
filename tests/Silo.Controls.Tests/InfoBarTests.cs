using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class InfoBarTests
{
    const string Sheet = """
        { "rules": {
          "Box": { "align-items": "start" },
          "InfoBar": { "flex-direction": "row", "padding": "5px", "border-width": "1px", "background": "#112233" },
          "InfoBar.closed": { "padding": "0px", "border-width": "0px" },
          "InfoBar .body": { "flex-direction": "column", "flex-grow": "1" }
        } }
        """;

    static Box Arrange(InfoBar bar)
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(bar);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(300, 100));

        return root;
    }

    [Fact]
    public void The_parts_in_use_are_shown_in_order()
    {
        var bar = new InfoBar { Title = "T", Message = "M", ActionText = "Go", IsClosable = true };

        Assert.Equal(3, bar.Children.Count);
    }

    [Fact]
    public void Title_action_and_close_are_left_out_when_unused()
    {
        var bar = new InfoBar { Message = "M" };

        Assert.Single(bar.Children);
    }

    [Fact]
    public void The_action_button_raises_the_event()
    {
        var bar = new InfoBar { Message = "M", ActionText = "Go" };
        var count = 0;

        bar.ActionInvoked += (_, _) => count++;

        var action = (Button)bar.Children[1];

        action.PointerDown();
        action.PointerUp(inside: true);

        Assert.Equal(1, count);
    }

    [Fact]
    public void The_close_button_closes_and_raises_closed()
    {
        var bar = new InfoBar { Message = "M", IsClosable = true };
        var closed = 0;

        bar.Closed += (_, _) => closed++;

        var close = (Button)bar.Children[1];

        close.PointerDown();
        close.PointerUp(inside: true);

        Assert.False(bar.IsOpen);
        Assert.Equal(1, closed);
        Assert.Contains("closed", bar.Classes);
        Assert.Empty(bar.Children);
    }

    [Fact]
    public void A_closed_bar_takes_no_space_and_draws_nothing()
    {
        var bar = new InfoBar { Message = "M" };

        bar.IsOpen = false;

        Arrange(bar);

        var builder = new DisplayListBuilder();

        bar.Render(builder);

        Assert.Equal(0, bar.Bounds.Height);
        Assert.DoesNotContain(builder.Build().Commands, c => c is FillRectCommand or StrokeRectCommand or DrawTextCommand);
    }

    [Fact]
    public void The_severity_sets_the_class()
    {
        var bar = new InfoBar();

        bar.Severity = Severity.Error;

        Assert.Contains("error", bar.Classes);
        Assert.DoesNotContain("info", bar.Classes);
    }
}

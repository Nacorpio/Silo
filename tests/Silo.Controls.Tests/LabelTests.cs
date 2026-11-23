using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class LabelTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "padding": "10px", "align-items": "start", "gap": "10px" },
            "Label": { "color": "#112233", "required-color": "#ff0000" }
          }
        }
        """;

    static (Box Root, Label Label, Control Target, PointerRouter Router) Setup(Control target)
    {
        var label = new Label { Text = "Name", For = target };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(label);
        root.Children.Add(target);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 200));

        return (root, label, target, new PointerRouter(root));
    }

    static List<DrawCommand> Frame(Box root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands];
    }

    [Fact]
    public void Pressing_the_label_toggles_a_check_box()
    {
        var (_, label, box, router) = Setup(new CheckBox { Text = "Remember" });

        router.Down(label.Bounds.X + 2, label.Bounds.Y + 2);
        router.Up(label.Bounds.X + 2, label.Bounds.Y + 2);

        Assert.True(((CheckBox)box).IsChecked);
    }

    [Fact]
    public void Pressing_a_label_without_a_target_does_nothing()
    {
        var label = new Label { Text = "Plain" };

        Assert.False(label.PointerDown());
    }

    [Fact]
    public void The_focus_goes_to_the_control_the_label_names()
    {
        var (_, label, box, _) = Setup(new TextBox());
        var focus = new FocusManager();

        Assert.True(focus.FocusFrom(label));
        Assert.Equal(box, focus.Focused);
    }

    [Fact]
    public void A_required_label_ends_in_a_marker_in_the_required_color()
    {
        var (root, label, _, _) = Setup(new TextBox());

        label.AddClass("required");
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 200));

        var marker = Frame(root).OfType<DrawTextCommand>().Single(c => c.Text == "*");
        var text = Frame(root).OfType<DrawTextCommand>().Single(c => c.Text == "Name");

        Assert.Equal(new Color(255, 0, 0), marker.Color);
        Assert.Equal(text.X + (4 * 7) + 2, marker.X);
        Assert.Equal(((4 + 1) * 7) + 2, label.Bounds.Width);
    }

    [Fact]
    public void A_label_that_is_not_required_has_no_marker()
    {
        var (root, _, _, _) = Setup(new TextBox());

        Assert.DoesNotContain(Frame(root), c => c is DrawTextCommand { Text: "*" });
    }
}

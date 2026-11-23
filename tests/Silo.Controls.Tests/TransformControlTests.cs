using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Xunit;

public class TransformControlTests
{
    const string Sheet = """
        {
          "rules": {
            "#root": { "width": "400px", "height": "300px", "align-items": "flex-start", "padding": "20px" },
            "#target": { "width": "100px", "height": "50px", "background": "#ff0000" },
            ".moved": { "transform": "translate(30px, 10px)" },
            ".turned": { "transform": "rotate(90deg)" },
            ".big": { "transform": "scale(2)" },
            ".corner": { "transform": "scale(2)", "transform-origin": "left top" },
            ".flat": { "transform": "scale(0)" }
          }
        }
        """;

    static (Box Root, Box Target) Setup(string? cls = null)
    {
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var target = new Box { Name = "target" };

        if (cls is not null)
            target.AddClass(cls);

        root.Children.Add(target);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        return (root, target);
    }

    static IReadOnlyList<DrawCommand> Commands(Control root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build().Commands;
    }

    [Fact]
    public void Without_a_transform_nothing_extra_is_recorded()
    {
        var (root, _) = Setup();

        Assert.DoesNotContain(Commands(root), command => command is ScaleCommand or RotateCommand);
    }

    [Fact]
    public void A_move_translates_the_paint_and_leaves_the_layout_alone()
    {
        var (root, target) = Setup("moved");

        Assert.Equal(new Rect(20, 20, 100, 50), target.Bounds);
        Assert.Contains(Commands(root), command => command is TranslateCommand(80, 35));
    }

    [Fact]
    public void Scale_and_rotation_are_applied_around_the_center_by_default()
    {
        var (root, _) = Setup("big");
        var commands = Commands(root);

        Assert.Contains(commands, command => command is ScaleCommand(2, 2));
        Assert.Contains(commands, command => command is TranslateCommand(50, 25));
        Assert.Contains(commands, command => command is TranslateCommand(-50, -25));

        var (turned, _) = Setup("turned");

        Assert.Contains(Commands(turned), command => command is RotateCommand(90));
    }

    [Fact]
    public void A_transform_origin_changes_where_the_Control_pivots()
    {
        var (root, _) = Setup("corner");

        Assert.Contains(Commands(root), command => command is TranslateCommand(0, 0) or ScaleCommand(2, 2));
        Assert.DoesNotContain(Commands(root), command => command is TranslateCommand(50, 25));
    }

    [Fact]
    public void Hit_testing_follows_a_move()
    {
        var (root, target) = Setup("moved");

        Assert.Same(target, root.HitTest(60, 40));
        Assert.Same(root, root.HitTest(25, 25));
        Assert.Same(target, root.HitTest(145, 75));
    }

    [Fact]
    public void Hit_testing_follows_a_scale_around_the_center()
    {
        var (root, target) = Setup("big");

        // The 100x50 box at (20, 20) now covers (-30, -5) to (170, 95).
        Assert.Same(target, root.HitTest(5, 2));
        Assert.Same(target, root.HitTest(165, 90));
        Assert.Same(root, root.HitTest(175, 90));
    }

    [Fact]
    public void Hit_testing_follows_a_rotation()
    {
        var (root, target) = Setup("turned");

        // Rotated a quarter turn around (70, 45): the box becomes 50 wide and 100 tall, so (70, 90) is inside and (115, 45) is not.
        Assert.Same(target, root.HitTest(70, 90));
        Assert.Same(root, root.HitTest(115, 45));
        Assert.Same(root, root.HitTest(30, 45));
    }

    [Fact]
    public void ToLocal_undoes_the_transform()
    {
        var (_, target) = Setup("big");

        var (x, y) = target.ToLocal(70, 45);

        Assert.Equal(50, x, 6);
        Assert.Equal(25, y, 6);

        (x, y) = target.ToLocal(170, 95);

        Assert.Equal(100, x, 6);
        Assert.Equal(50, y, 6);
    }

    [Fact]
    public void The_transform_of_a_parent_carries_its_children()
    {
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };
        var parent = new Box { Name = "parent" };
        var child = new Box { Name = "child" };

        parent.AddClass("big");
        child.AddClass("kid");
        parent.Children.Add(child);
        root.Children.Add(parent);

        const string sheet = """
            { "rules": {
              "#root": { "width": "400px", "height": "300px", "align-items": "flex-start" },
              "#parent": { "width": "100px", "height": "100px", "padding": "10px", "align-items": "flex-start" },
              "#child": { "width": "20px", "height": "20px" },
              ".big": { "transform": "scale(2)", "transform-origin": "left top" }
            } }
            """;

        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read(sheet, "t.jsonc")]));
        root.Arrange(new Size(400, 300));

        // The child sits at (10, 10) to (30, 30) inside the parent, which is drawn twice as large from its corner.
        Assert.Same(child, root.HitTest(35, 35));
        Assert.Same(parent, root.HitTest(65, 65));

        var (x, y) = child.ToLocal(40, 40);

        Assert.Equal(10, x, 6);
        Assert.Equal(10, y, 6);
    }

    [Fact]
    public void A_Control_scaled_to_nothing_cannot_be_hit()
    {
        var (root, target) = Setup("flat");

        Assert.NotSame(target, root.HitTest(70, 45));
    }

    [Fact]
    public void The_property_is_set_from_C_sharp_like_any_other_styled_value()
    {
        var (root, target) = Setup();

        target.SetValue(Control.TransformProperty, new Silo.Styling.Values.Transform(30, 10, 0, 1, 1));

        Assert.Same(target, root.HitTest(60, 40));
    }
}

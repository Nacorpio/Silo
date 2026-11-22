using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Styling.Values;
using Xunit;

public class ControlTests
{
    static StyleResolver Resolver(string json) => new([JsoncStylesheetReader.Read(json, "t.jsonc")]);

    static Box Root(params Control[] children)
    {
        var root = new Box { Name = "root", TextMeasurer = new FixedMeasurer() };

        foreach (var child in children)
            root.Children.Add(child);

        return root;
    }

    static DisplayList Frame(Control root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return builder.Build();
    }

    [Fact]
    public void Stylesheet_flex_properties_position_the_children()
    {
        var a = new Box { Name = "a" };
        var b = new Box { Name = "b" };

        var root = Root(a, b);

        root.ApplyStyles(Resolver("""
            {
              "rules": {
                "#root": { "gap": "10px", "padding": "5px", "flex-direction": "row" },
                "#a": { "width": "40px", "height": "20px" },
                "#b": { "flex-grow": "1" }
              }
            }
            """));

        root.Arrange(new Size(200, 100));

        Assert.Equal(new Rect(5, 5, 40, 20), a.Bounds);
        Assert.Equal(new Rect(55, 5, 140, 90), b.Bounds);
    }

    [Fact]
    public void Column_direction_and_alignment_come_from_the_stylesheet()
    {
        var child = new Box { Name = "child" };

        var root = Root(child);

        root.ApplyStyles(Resolver("""{ "rules": { "#root": { "flex-direction": "column", "align-items": "center" }, "#child": { "width": "30px", "height": "10px" } } }"""));
        root.Arrange(new Size(100, 100));

        Assert.Equal(new Rect(35, 0, 30, 10), child.Bounds);
    }

    [Fact]
    public void Rendering_paints_background_and_border_before_the_children()
    {
        var child = new Box { Name = "child" };

        var root = Root(child);

        root.ApplyStyles(Resolver("""
            {
              "rules": {
                "#root": { "background": "#ff0000", "border-width": "2px", "border-color": "#00ff00", "border-radius": "4px", "padding": "10px" },
                "#child": { "background": "#0000ff", "width": "20px", "height": "20px" }
              }
            }
            """));

        root.Arrange(new Size(100, 60));

        var commands = Frame(root).Commands;

        Assert.Contains(new FillRectCommand(new Rect(0, 0, 100, 60), new Color(255, 0, 0), 4), commands);
        Assert.Contains(new StrokeRectCommand(new Rect(1, 1, 98, 58), new Color(0, 255, 0), 2, 4), commands);
        Assert.Contains(new FillRectCommand(new Rect(0, 0, 20, 20), new Color(0, 0, 255), 0), commands);
        var list = commands.ToList();
        var red = list.FindIndex(c => c is FillRectCommand { Color.R: 255 });
        var blue = list.FindIndex(c => c is FillRectCommand { Color.B: 255 });

        Assert.True(red < blue);
    }

    [Fact]
    public void Transparent_background_and_border_paint_nothing()
    {
        var root = Root();

        root.Arrange(new Size(10, 10));

        Assert.DoesNotContain(Frame(root).Commands, c => c is FillRectCommand or StrokeRectCommand);
    }

    [Fact]
    public void Text_size_comes_from_the_measurer_and_is_drawn_in_the_content_box()
    {
        var text = new TextBlock { Text = "Hello" };

        var root = Root(text);

        root.ApplyStyles(Resolver("""{ "rules": { "#root": { "align-items": "start" }, "TextBlock": { "padding": "4px", "color": "#112233" } } }"""));
        root.Arrange(new Size(300, 100));

        Assert.Equal(new Rect(0, 0, 43, 22), text.Bounds);
        var expected = new DrawTextCommand("Hello", 4, 4, new TextStyle("Segoe UI", 14), new Color(17, 34, 51));

        Assert.Contains(expected, Frame(root).Commands);
    }

    [Fact]
    public void Font_and_color_properties_inherit_from_ancestors()
    {
        var text = new TextBlock { Text = "Hi" };

        var root = Root(text);

        root.ApplyStyles(Resolver("""{ "rules": { "#root": { "color": "#ff0000", "font-size": "20px", "font-weight": "bold", "font-family": "Inter" } } }"""));
        root.Arrange(new Size(100, 50));

        var expected = new DrawTextCommand("Hi", 0, 18, new TextStyle("Inter", 20, true), new Color(255, 0, 0));

        Assert.Contains(expected, Frame(root).Commands);
    }

    [Fact]
    public void Text_align_center_moves_the_text_inside_the_content_box()
    {
        var text = new TextBlock { Text = "Hi" };

        var root = Root(text);

        root.ApplyStyles(Resolver("""{ "rules": { "TextBlock": { "text-align": "center", "width": "100px", "height": "30px" } } }"""));
        root.Arrange(new Size(200, 100));

        var expected = new DrawTextCommand("Hi", 43, 8, new TextStyle("Segoe UI", 14), new Color(0, 0, 0));

        Assert.Contains(expected, Frame(root).Commands);
    }

    [Fact]
    public void Measuring_text_without_a_measurer_explains_what_to_set()
    {
        var root = new Box();

        root.Children.Add(new TextBlock { Text = "x" });

        var error = Assert.Throws<InvalidOperationException>(() => root.Arrange(new Size(10, 10)));

        Assert.Contains("TextMeasurer", error.Message);
    }

    [Fact]
    public void A_child_that_is_not_a_control_is_rejected_by_layout()
    {
        var root = Root();

        root.Children.Add(new Element());

        Assert.Throws<InvalidOperationException>(() => root.Arrange(new Size(10, 10)));
    }

    [Fact]
    public void Hit_testing_returns_the_deepest_control_and_the_topmost_sibling()
    {
        var inner = new Box { Name = "inner" };
        var outer = new Box { Name = "outer" };

        var root = Root(outer);

        outer.Children.Add(inner);

        root.ApplyStyles(Resolver("""{ "rules": { "#outer": { "width": "100px", "height": "100px", "padding": "10px" }, "#inner": { "flex-grow": "1" } } }"""));
        root.Arrange(new Size(200, 200));

        Assert.Same(inner, root.HitTest(50, 50));
        Assert.Same(outer, root.HitTest(5, 5));
        Assert.Same(root, root.HitTest(150, 150));
        Assert.Null(root.HitTest(300, 300));
    }

    [Fact]
    public void Invalid_control_property_values_are_reported_with_their_source_position()
    {
        var root = Root();

        var diagnostics = root.ApplyStyles(Resolver("""{ "rules": { "Box": { "flex-direction": "diagonal", "width": "wide" } } }"""));

        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, d => Assert.Equal("t.jsonc", d.Span.File));
    }
}

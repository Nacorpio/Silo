using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class PointerRouterTests
{
    static (Box Root, Button Button, PointerRouter Router) Setup()
    {
        var button = new Button { Text = "Go" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(button);
        root.ApplyStyles(new StyleResolver([JsoncStylesheetReader.Read("""{ "rules": { "Box": { "align-items": "start" }, "Button": { "padding": "5px" } } }""", "t.jsonc")]));
        root.Arrange(new Size(200, 100));

        return (root, button, new PointerRouter(root));
    }

    [Fact]
    public void Hover_follows_the_pointer_and_includes_ancestors()
    {
        var (root, button, router) = Setup();

        Assert.True(router.Move(5, 5));
        Assert.Same(button, router.Hovered);
        Assert.Contains("hover", button.PseudoStates);
        Assert.Contains("hover", root.PseudoStates);

        Assert.True(router.Move(150, 90));
        Assert.DoesNotContain("hover", button.PseudoStates);
        Assert.Contains("hover", root.PseudoStates);

        Assert.True(router.Leave());
        Assert.DoesNotContain("hover", root.PseudoStates);
    }

    [Fact]
    public void Moving_within_the_same_control_changes_nothing()
    {
        var (_, _, router) = Setup();

        router.Move(5, 5);

        Assert.False(router.Move(6, 6));
    }

    [Fact]
    public void Press_and_release_over_a_button_clicks_it()
    {
        var (_, button, router) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        router.Down(5, 5);

        Assert.Contains("active", button.PseudoStates);

        router.Up(5, 5);

        Assert.Equal(1, clicks);
        Assert.DoesNotContain("active", button.PseudoStates);
    }

    [Fact]
    public void Releasing_outside_the_button_releases_it_without_a_click()
    {
        var (_, button, router) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        router.Down(5, 5);
        router.Up(150, 90);

        Assert.Equal(0, clicks);
        Assert.DoesNotContain("active", button.PseudoStates);
    }

    [Fact]
    public void Pressing_somewhere_else_does_not_click_a_button_on_release()
    {
        var (_, button, router) = Setup();
        var clicks = 0;

        button.Click += (_, _) => clicks++;

        router.Down(150, 90);
        router.Up(5, 5);

        Assert.Equal(0, clicks);
    }
}

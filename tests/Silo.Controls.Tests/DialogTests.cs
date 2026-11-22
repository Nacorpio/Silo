using Silo;
using Silo.Layout;
using Silo.Styling;
using Xunit;

public class DialogTests
{
    const string Sheet = """
        {
          "rules": {
            "Box": { "width": "400px", "height": "300px" },
            "Dialog": { "flex-direction": "column", "width": "200px", "gap": "4px" },
            "DialogButtons": { "flex-direction": "row", "gap": "4px" }
          }
        }
        """;

    static (Box Root, Dialog Dialog, OverlayLayer Layer, PointerRouter Router) Setup()
    {
        var root = new Box { TextMeasurer = new FixedMeasurer() };
        var layer = new OverlayLayer { TextMeasurer = root.TextMeasurer };
        var resolver = new StyleResolver([JsoncStylesheetReader.Read(Sheet, "t.jsonc")]);

        var dialog = new Dialog { Title = "Delete", Content = new TextBlock { Text = "Sure?" } };

        dialog.AddButton("Cancel", "cancel");
        dialog.AddButton("Delete");

        root.Overlays = layer;
        root.ApplyStyles(resolver);
        root.Arrange(new Size(400, 300));

        layer.Changed += (_, _) => layer.ApplyStyles(resolver);

        return (root, dialog, layer, new PointerRouter(root, layer));
    }

    static void Layout(OverlayLayer layer) => layer.Arrange(new Size(400, 300));

    static (double X, double Y) Center(Control control)
    {
        var (x, y) = control.ToLocal(0, 0);

        return (-x + (control.Bounds.Width / 2), -y + (control.Bounds.Height / 2));
    }

    [Fact]
    public void A_Dialog_stacks_its_title_content_and_buttons_and_leaves_out_the_empty_ones()
    {
        var (_, dialog, _, _) = Setup();

        Assert.Equal(3, dialog.Children.Count);
        Assert.Same(dialog.TitleControl, dialog.Children[0]);
        Assert.Same(dialog.ButtonRow, dialog.Children[2]);

        dialog.Title = "";
        dialog.Content = null;

        Assert.Single(dialog.Children);
    }

    [Fact]
    public void Show_opens_it_in_the_middle_of_the_window()
    {
        var (root, dialog, layer, _) = Setup();

        Assert.True(dialog.Show(root));
        Layout(layer);

        Assert.True(dialog.IsOpen);

        var (x, y) = Center(dialog);

        Assert.Equal(200, x, 1);
        Assert.Equal(150, y, 1);
    }

    [Fact]
    public void Show_does_nothing_when_it_is_open_or_the_anchor_is_not_in_a_window()
    {
        var (root, dialog, _, _) = Setup();

        Assert.False(dialog.Show(new Box()));

        dialog.Show(root);

        Assert.False(dialog.Show(root));
    }

    [Fact]
    public void Pressing_a_button_closes_it_with_the_result_of_the_button()
    {
        var (root, dialog, layer, router) = Setup();
        var results = new List<object?>();

        dialog.Closed += (_, e) => results.Add(e.Result);

        dialog.Show(root);
        Layout(layer);

        var cancel = (Button)dialog.ButtonRow.Children[0];
        var (x, y) = Center(cancel);

        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(["cancel"], results);
        Assert.False(dialog.IsOpen);
    }

    [Fact]
    public void A_button_without_a_result_reports_its_text()
    {
        var (root, dialog, layer, router) = Setup();
        var results = new List<object?>();

        dialog.Closed += (_, e) => results.Add(e.Result);

        dialog.Show(root);
        Layout(layer);

        var (x, y) = Center((Button)dialog.ButtonRow.Children[1]);

        router.Down(x, y);
        router.Up(x, y);

        Assert.Equal(["Delete"], results);
    }

    [Fact]
    public void A_press_outside_the_Dialog_is_swallowed_and_the_Dialog_stays()
    {
        var (root, dialog, layer, router) = Setup();

        dialog.Show(root);
        Layout(layer);

        router.Down(5, 5);
        router.Up(5, 5);

        Assert.True(dialog.IsOpen);
    }

    [Fact]
    public void Escape_dismisses_it()
    {
        var (root, dialog, layer, _) = Setup();
        var closed = new List<DialogClosedEventArgs>();

        dialog.Closed += (_, e) => closed.Add(e);

        dialog.Show(root);

        Assert.True(layer.CloseTop());

        Assert.False(dialog.IsOpen);
        Assert.True(closed[0].IsDismissed);
    }

    [Fact]
    public void It_can_be_shown_again_after_it_closed()
    {
        var (root, dialog, layer, _) = Setup();

        dialog.Show(root);
        dialog.Close("done");

        Assert.True(dialog.Show(root));
        Layout(layer);

        Assert.True(dialog.IsOpen);
    }

    [Fact]
    public void Close_does_nothing_while_it_is_not_open()
    {
        var (_, dialog, _, _) = Setup();
        var raised = 0;

        dialog.Closed += (_, _) => raised++;

        Assert.False(dialog.Close("x"));
        Assert.Equal(0, raised);
    }
}

using Silo;
using Silo.Layout;
using Silo.Rendering;
using Silo.Styling;
using Silo.Validation;
using Xunit;

public class ChatInputTests
{
    static ChatInput Typed(string text)
    {
        var input = new ChatInput();

        input.Input.TextInput(text);

        return input;
    }

    [Fact]
    public void Enter_sends_the_trimmed_text_and_clears_the_box()
    {
        var input = Typed("  hello  ");
        var sent = new List<string>();

        input.Submitted += (_, text) => sent.Add(text);

        Assert.True(input.Input.KeyDown(Key.Enter, KeyModifiers.None));

        Assert.Equal(["hello"], sent);
        Assert.Equal(string.Empty, input.Text);
    }

    [Fact]
    public void Shift_Enter_breaks_the_line_and_sends_nothing()
    {
        var input = Typed("a");
        var raised = 0;

        input.Submitted += (_, _) => raised++;
        input.Input.KeyDown(Key.Enter, KeyModifiers.Shift);
        input.Input.TextInput("b");

        Assert.Equal(0, raised);
        Assert.Equal("a\nb", input.Text);
    }

    [Fact]
    public void A_message_of_white_space_is_not_sent()
    {
        var input = Typed("   ");
        var raised = 0;

        input.Submitted += (_, _) => raised++;

        Assert.False(input.CanSend);
        Assert.False(input.Send());
        Assert.False(input.SendButton.PointerDown() && input.SendButton.PointerUp(true) && raised > 0);
        Assert.Equal(0, raised);
        Assert.Equal("   ", input.Text);
    }

    [Fact]
    public void Without_SendOnEnter_Ctrl_Enter_sends_and_Enter_breaks_the_line()
    {
        var input = Typed("a") ;
        var sent = new List<string>();

        input.SendOnEnter = false;
        input.Submitted += (_, text) => sent.Add(text);

        input.Input.KeyDown(Key.Enter, KeyModifiers.None);

        Assert.Empty(sent);
        Assert.Equal("a\n", input.Text);

        input.Input.TextInput("b");
        input.Input.KeyDown(Key.Enter, KeyModifiers.Control);

        Assert.Equal(["a\nb"], sent);
    }

    [Fact]
    public void The_Send_button_sends()
    {
        var input = Typed("hi");
        var sent = new List<string>();

        input.Submitted += (_, text) => sent.Add(text);
        input.SendButton.PointerDown();
        input.SendButton.PointerUp(true);

        Assert.Equal(["hi"], sent);
    }

    [Fact]
    public void The_context_bar_shows_while_set_and_Escape_or_its_button_closes_it()
    {
        var input = new ChatInput();
        var cleared = 0;

        input.ContextCleared += (_, _) => cleared++;

        Assert.False(input.Input.KeyDown(Key.Escape, KeyModifiers.None));
        Assert.Single(input.Children);

        input.SetContext("Replying to Dana");

        Assert.Equal("Replying to Dana", input.Context);
        Assert.Equal(2, input.Children.Count);
        Assert.True(input.Input.KeyDown(Key.Escape, KeyModifiers.None));
        Assert.Null(input.Context);
        Assert.Equal(1, cleared);
        Assert.Single(input.Children);

        input.SetContext("Editing");

        var close = Descendants(input).OfType<Button>().Single(b => b.HasClass("close"));

        close.PointerDown();
        close.PointerUp(true);

        Assert.Null(input.Context);
        Assert.Equal(2, cleared);
    }

    [Fact]
    public void The_Send_button_and_the_attachment_slot_can_be_left_out_or_filled()
    {
        var input = new ChatInput { SendButtonMode = SendButtonMode.Always };
        var tray = new Box();

        Assert.Contains(input.SendButton, Descendants(input));

        input.SendButtonMode = SendButtonMode.Never;

        Assert.DoesNotContain(input.SendButton, Descendants(input));

        input.Attachments = tray;

        Assert.Contains(tray, Descendants(input));

        input.Attachments = null;

        Assert.DoesNotContain(tray, Descendants(input));
    }

    [Fact]
    public void The_Send_button_can_show_only_while_there_is_text_and_its_text_can_change()
    {
        var input = new ChatInput { SendText = "Post" };

        Assert.Equal(SendButtonMode.WhenText, input.SendButtonMode);

        Assert.DoesNotContain(input.SendButton, Descendants(input));

        input.Input.TextInput("hi");

        Assert.Contains(input.SendButton, Descendants(input));
        Assert.Equal("Post", input.SendButton.Text);

        input.Send();

        Assert.DoesNotContain(input.SendButton, Descendants(input));

        input.SendButtonMode = SendButtonMode.Always;

        Assert.Contains(input.SendButton, Descendants(input));
    }

    [Fact]
    public void The_density_is_a_Class_for_the_theme()
    {
        var input = new ChatInput { Density = ChatDensity.Roomy };

        Assert.True(input.HasClass("density-roomy"));

        input.Density = ChatDensity.Compact;

        Assert.True(input.HasClass("density-compact"));
        Assert.False(input.HasClass("density-roomy"));

        input.Density = ChatDensity.Normal;

        Assert.False(input.HasClass("density-compact"));
    }

    [Fact]
    public void The_box_grows_with_the_lines_up_to_the_maximum_and_then_stops()
    {
        var input = new ChatInput { MaximumLines = 3 };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(input);
        root.ApplyStyles(new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), ChatTheme.Load()]));
        root.Arrange(new Size(400, 400));

        var one = input.Input.Bounds.Height;

        input.Text = "a\nb";
        root.Arrange(new Size(400, 400));

        var two = input.Input.Bounds.Height;

        input.Text = "a\nb\nc\nd\ne";
        root.Arrange(new Size(400, 400));

        Assert.Equal(one + 14, two);
        Assert.Equal(one + 28, input.Input.Bounds.Height);
    }

    [Fact]
    public void A_long_line_wraps_and_makes_the_box_taller()
    {
        var input = new ChatInput { MaximumLines = 8 };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(input);
        root.ApplyStyles(new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), ChatTheme.Load()]));
        root.Arrange(new Size(300, 400));

        var one = input.Input.Bounds.Height;

        input.Text = string.Join(' ', Enumerable.Repeat("word", 30));
        root.Arrange(new Size(300, 400));

        Assert.True(input.Input.WordWrap);
        Assert.True(input.Input.Bounds.Height > one);
    }

    [Fact]
    public void The_placeholder_is_drawn_only_while_the_box_is_empty()
    {
        var input = new ChatInput { Placeholder = "Message" };
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(input);
        root.Arrange(new Size(400, 400));

        Assert.Contains("Message", DrawnText(root));

        input.Text = "x";

        Assert.DoesNotContain("Message", DrawnText(root));
    }

    [Fact]
    public void The_chat_theme_is_valid_against_the_default_theme()
    {
        StylesheetValidator.AddControls(typeof(ChatInput).Assembly);

        var issues = StylesheetValidator.ValidateText(ChatTheme.Source(), ChatTheme.FileName, [DefaultTheme.Load()]);

        Assert.Empty(issues);
    }

    [Fact]
    public void A_themed_input_draws_the_Send_button_after_the_text_area()
    {
        var input = Typed("hi");
        var root = new Box { TextMeasurer = new FixedMeasurer() };

        root.Children.Add(input);
        root.ApplyStyles(new StyleResolver([.. DefaultTheme.Load(ThemeVariant.Dark), ChatTheme.Load()]));
        root.Arrange(new Size(500, 200));

        Assert.True(input.SendButton.Bounds.X >= input.Input.Bounds.Right);
        Assert.True(input.Input.Bounds.Width > 200);
    }

    static List<string> DrawnText(Control root)
    {
        var builder = new DisplayListBuilder();

        root.Render(builder);

        return [.. builder.Build().Commands.OfType<DrawTextCommand>().Select(c => c.Text)];
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children.OfType<Control>())
        {
            yield return child;

            foreach (var inner in Descendants(child))
                yield return inner;
        }
    }
}

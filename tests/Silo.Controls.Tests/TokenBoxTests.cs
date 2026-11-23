using Silo;
using Xunit;

public class TokenBoxTests
{
    static void Type(TokenBox box, string text)
    {
        box.Entry.Text = string.Empty;
        box.Entry.TextInput(text);
    }

    [Fact]
    public void Enter_turns_the_text_into_a_token_and_clears_the_text_box()
    {
        var box = new TokenBox();

        Type(box, "bug");
        box.Entry.KeyDown(Key.Enter);

        Assert.Equal(["bug"], box.Tokens);
        Assert.Equal(string.Empty, box.Entry.Text);
    }

    [Fact]
    public void A_comma_commits_the_text_before_it()
    {
        var box = new TokenBox();

        Type(box, "docs,");

        Assert.Equal(["docs"], box.Tokens);
        Assert.Equal(string.Empty, box.Entry.Text);
    }

    [Fact]
    public void Blank_text_and_duplicates_are_ignored_unless_duplicates_are_allowed()
    {
        var box = new TokenBox();

        Assert.False(box.Add("  "));
        Assert.True(box.Add("a"));
        Assert.False(box.Add("A"));

        box.AllowDuplicates = true;

        Assert.True(box.Add("a"));
        Assert.Equal(2, box.Tokens.Count);
    }

    [Fact]
    public void Backspace_in_an_empty_text_box_removes_the_last_token_but_not_in_a_filled_one()
    {
        var box = new TokenBox();

        box.Add("a");
        box.Add("b");

        box.Entry.TextInput("x");
        box.Entry.KeyDown(Key.Backspace);

        Assert.Equal(2, box.Tokens.Count);

        box.Entry.KeyDown(Key.Backspace);

        Assert.Equal(["a"], box.Tokens);
    }

    [Fact]
    public void The_remove_button_of_a_chip_removes_that_token()
    {
        var box = new TokenBox();

        box.Add("a");
        box.Add("b");
        box.Add("c");

        var chip = (Control)box.Children[1];
        var remove = (Button)chip.Children[1];

        remove.PointerDown();
        remove.PointerUp(inside: true);

        Assert.Equal(["a", "c"], box.Tokens);
        Assert.Equal(3, box.Children.Count);
    }

    [Fact]
    public void Enter_completes_to_the_only_matching_suggestion()
    {
        var box = new TokenBox { Suggestions = ["bug", "feature", "fix"] };

        Type(box, "BU");
        box.Entry.KeyDown(Key.Enter);

        Type(box, "f");
        box.Entry.KeyDown(Key.Enter);

        Assert.Equal(["bug", "f"], box.Tokens);
    }

    [Fact]
    public void Restricting_to_suggestions_drops_other_text()
    {
        var box = new TokenBox { Suggestions = ["bug"], RestrictToSuggestions = true };

        Type(box, "nope");
        box.Entry.KeyDown(Key.Enter);

        Assert.Empty(box.Tokens);
        Assert.Equal(string.Empty, box.Entry.Text);
    }

    [Fact]
    public void Changing_tokens_raises_the_event_and_the_text_box_stays_last()
    {
        var box = new TokenBox();
        var count = 0;

        box.TokensChanged += (_, _) => count++;

        box.Add("a");
        box.Add("b");
        box.Clear();

        Assert.Equal(3, count);
        Assert.Same(box.Entry, box.Children[^1]);
        Assert.Single(box.Children);
    }
}

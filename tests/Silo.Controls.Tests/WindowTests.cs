using Silo;
using Xunit;

public class WindowTests
{
    [Fact]
    public void A_new_Window_has_no_title_and_opens_at_800_by_600()
    {
        var window = new Window();

        Assert.Equal(string.Empty, window.Title);
        Assert.Equal(800, window.InitialWidth);
        Assert.Equal(600, window.InitialHeight);
    }

    [Fact]
    public void Changing_the_title_raises_TitleChanged_and_setting_the_same_title_does_not()
    {
        var window = new Window { Title = "Notes" };
        var raised = 0;

        window.TitleChanged += (_, _) => raised++;

        window.Title = "Notes";

        Assert.Equal(0, raised);

        window.Title = "Todo";

        Assert.Equal(1, raised);
        Assert.Equal("Todo", window.Title);
    }

    [Fact]
    public void A_null_title_becomes_an_empty_string()
    {
        var window = new Window { Title = "Notes" };

        window.Title = null!;

        Assert.Equal(string.Empty, window.Title);
    }
}

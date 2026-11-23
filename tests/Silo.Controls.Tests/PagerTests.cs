using Silo;
using Xunit;

public class PagerTests
{
    static List<string> Texts(Pager pager) => [.. pager.Children.Select(c => ((TextControl)c).Text)];

    [Fact]
    public void Few_pages_are_all_shown_between_the_arrows()
    {
        var pager = new Pager { PageCount = 4 };

        Assert.Equal(["‹", "1", "2", "3", "4", "›"], Texts(pager));
    }

    [Fact]
    public void Pages_far_from_the_current_one_collapse_into_ellipses()
    {
        var pager = new Pager { PageCount = 20, CurrentPage = 10 };

        Assert.Equal(["‹", "1", "…", "9", "10", "11", "…", "20", "›"], Texts(pager));
    }

    [Fact]
    public void A_single_hidden_page_is_shown_instead_of_an_ellipsis()
    {
        var pager = new Pager { PageCount = 20, CurrentPage = 4 };

        Assert.Equal(["‹", "1", "2", "3", "4", "5", "…", "20", "›"], Texts(pager));
    }

    [Fact]
    public void Clicking_a_page_and_the_arrows_change_the_page()
    {
        var pager = new Pager { PageCount = 5, CurrentPage = 2 };
        var raised = 0;

        pager.PageChanged += (_, _) => raised++;

        var next = (Button)pager.Children[^1];

        next.PointerDown();
        next.PointerUp(inside: true);

        Assert.Equal(3, pager.CurrentPage);

        var five = (Button)pager.Children[^2];

        five.PointerDown();
        five.PointerUp(inside: true);

        Assert.Equal(5, pager.CurrentPage);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void The_current_page_and_unusable_arrows_have_classes()
    {
        var pager = new Pager { PageCount = 3, CurrentPage = 1 };

        Assert.Contains("disabled", ((Control)pager.Children[0]).Classes);
        Assert.DoesNotContain("disabled", ((Control)pager.Children[^1]).Classes);
        Assert.Contains("current", ((Control)pager.Children[1]).Classes);
    }

    [Fact]
    public void The_page_stays_in_range_and_the_keys_move_it()
    {
        var pager = new Pager { PageCount = 5, CurrentPage = 9 };

        Assert.Equal(5, pager.CurrentPage);

        pager.PageCount = 3;

        Assert.Equal(3, pager.CurrentPage);

        Assert.False(pager.KeyDown(Key.Right));
        Assert.True(pager.KeyDown(Key.Left));
        Assert.True(pager.KeyDown(Key.Home));
        Assert.Equal(1, pager.CurrentPage);
        Assert.True(pager.KeyDown(Key.End));
        Assert.Equal(3, pager.CurrentPage);
    }
}

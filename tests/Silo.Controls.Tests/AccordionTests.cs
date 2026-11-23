using Silo;
using Xunit;

public class AccordionTests
{
    static Accordion Create()
    {
        var accordion = new Accordion();

        accordion.Add("One", new TextBlock { Text = "1" });
        accordion.Add("Two", new TextBlock { Text = "2" });
        accordion.Add("Three", new TextBlock { Text = "3" });

        return accordion;
    }

    [Fact]
    public void Opening_one_closes_the_others_by_default()
    {
        var accordion = Create();

        accordion.Sections[0].IsExpanded = true;
        accordion.Sections[2].IsExpanded = true;

        Assert.False(accordion.Sections[0].IsExpanded);
        Assert.True(accordion.Sections[2].IsExpanded);
        Assert.Equal(2, accordion.ExpandedIndex);
    }

    [Fact]
    public void Several_can_be_open_when_allowed()
    {
        var accordion = Create();

        accordion.AllowMultiple = true;
        accordion.Sections[0].IsExpanded = true;
        accordion.Sections[1].IsExpanded = true;

        Assert.True(accordion.Sections[0].IsExpanded);
        Assert.True(accordion.Sections[1].IsExpanded);
    }

    [Fact]
    public void Turning_multiple_off_keeps_only_the_first_open_one()
    {
        var accordion = Create();

        accordion.AllowMultiple = true;
        accordion.Sections[1].IsExpanded = true;
        accordion.Sections[2].IsExpanded = true;
        accordion.AllowMultiple = false;

        Assert.True(accordion.Sections[1].IsExpanded);
        Assert.False(accordion.Sections[2].IsExpanded);
    }

    [Fact]
    public void The_index_opens_one_and_closes_the_rest_and_minus_one_closes_all()
    {
        var accordion = Create();

        accordion.ExpandedIndex = 1;

        Assert.Equal(1, accordion.ExpandedIndex);

        accordion.ExpandedIndex = -1;

        Assert.Equal(-1, accordion.ExpandedIndex);
    }

    [Fact]
    public void The_event_is_raised_once_per_user_change()
    {
        var accordion = Create();
        var raised = 0;

        accordion.ExpandedChanged += (_, _) => raised++;

        accordion.Sections[0].IsExpanded = true;
        accordion.Sections[1].IsExpanded = true;

        Assert.Equal(2, raised);
    }
}

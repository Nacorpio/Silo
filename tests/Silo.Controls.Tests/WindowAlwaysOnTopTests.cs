using Silo;
using Xunit;

public class WindowAlwaysOnTopTests
{
    sealed class FakeHost : IWindowHost
    {
        public bool IsMaximized => false;

        public bool AlwaysOnTop { get; set; }

        public void Minimize()
        {
        }

        public void Maximize()
        {
        }

        public void Restore()
        {
        }

        public void Close()
        {
        }
    }

    [Fact]
    public void A_window_is_not_always_on_top_by_default()
    {
        Assert.False(new Window().AlwaysOnTop);
    }

    [Fact]
    public void The_value_is_remembered_before_the_platform_window_opens_and_handed_to_the_host()
    {
        var window = new Window { AlwaysOnTop = true };

        Assert.True(window.AlwaysOnTop);

        var host = new FakeHost();

        window.Host = host;

        Assert.True(host.AlwaysOnTop);
    }

    [Fact]
    public void Changing_the_value_while_the_window_is_open_changes_the_host_and_reads_back_from_it()
    {
        var host = new FakeHost();
        var window = new Window { Host = host };

        window.AlwaysOnTop = true;

        Assert.True(host.AlwaysOnTop);

        host.AlwaysOnTop = false;

        Assert.False(window.AlwaysOnTop);
    }
}

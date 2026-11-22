using Silo;
using Silo.Studio;
using Xunit;

public class PreviewsPanelTests
{
    sealed class FakeProcess : IPreviewProcess
    {
        public event EventHandler? Exited;

        public bool HasExited { get; private set; }

        public string Error { get; set; } = string.Empty;

        public int? ExitCode { get; private set; }

        public bool Disposed { get; private set; }

        public void End(int code)
        {
            HasExited = true;
            ExitCode = code;

            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Stop() => HasExited = true;

        public void Dispose()
        {
            Disposed = true;

            Stop();
        }
    }

    sealed class FakeLauncher(params string[] names) : IPreviewLauncher
    {
        public List<(string Name, ThemeVariant Variant, string? Theme)> Started { get; } = [];

        public List<FakeProcess> Processes { get; } = [];

        public string? Failure { get; set; }

        public IReadOnlyList<string> List(string assemblyPath)
            => Failure is null ? names : throw new InvalidOperationException(Failure);

        public IPreviewProcess Start(string assemblyPath, string name, ThemeVariant variant, string? theme)
        {
            var process = new FakeProcess();

            Started.Add((name, variant, theme));
            Processes.Add(process);

            return process;
        }
    }

    static T Find<T>(Element root, string name) where T : Element
    {
        var found = new Queue<Element>([root]);

        while (found.Count > 0)
        {
            var next = found.Dequeue();

            if (next is T match && match.Name == name)
                return match;

            foreach (var child in next.Children)
                found.Enqueue(child);
        }

        throw new InvalidOperationException(name);
    }

    static void Press(Button button)
    {
        button.PointerDown();
        button.PointerUp(inside: true);
    }

    static string StatusOf(PreviewsPanel panel) => Find<TextBlock>(panel, "preview-status").Text;

    [Fact]
    public void The_panel_lists_the_Previews_of_the_assembly_and_shows_nothing_yet()
    {
        var launcher = new FakeLauncher("Primary", "Quiet");
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Light);

        Assert.Equal(["Primary", "Quiet"], panel.Names);
        Assert.Equal(["Primary", "Quiet"], Find<ListView>(panel, "previews").Items);
        Assert.Empty(launcher.Started);
        Assert.Equal("2 Preview(s)", StatusOf(panel));
    }

    [Fact]
    public void Picking_a_Preview_starts_it_with_the_variant_and_theme_and_picking_another_ends_the_first()
    {
        var launcher = new FakeLauncher("Primary", "Quiet");
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Dark, "theme.jsonc");
        var list = Find<ListView>(panel, "previews");

        list.SelectedIndex = 0;
        list.SelectedIndex = 1;

        Assert.Equal(
            [("Primary", ThemeVariant.Dark, "theme.jsonc"), ("Quiet", ThemeVariant.Dark, "theme.jsonc")],
            launcher.Started);
        Assert.True(launcher.Processes[0].Disposed);
        Assert.True(panel.IsRunning);
    }

    [Fact]
    public void Show_starts_the_picked_Preview_again()
    {
        var launcher = new FakeLauncher("Primary");
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Light);

        Find<ListView>(panel, "previews").SelectedIndex = 0;

        panel.Show();

        Assert.Equal(2, launcher.Started.Count);
        Assert.True(launcher.Processes[0].Disposed);
    }

    [Fact]
    public void A_Preview_that_failed_says_why_and_one_that_was_closed_says_so()
    {
        var launcher = new FakeLauncher("Primary");
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Light);

        Find<ListView>(panel, "previews").SelectedIndex = 0;

        launcher.Processes[0].Error = "The sample data is missing.";
        launcher.Processes[0].End(2);

        Assert.Equal("Primary failed: The sample data is missing.", StatusOf(panel));

        panel.Show();

        launcher.Processes[1].End(0);

        Assert.Equal("Primary was closed", StatusOf(panel));
    }

    [Fact]
    public void A_Preview_that_ends_after_it_was_replaced_does_not_change_the_status()
    {
        var launcher = new FakeLauncher("Primary", "Quiet");
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Light);
        var list = Find<ListView>(panel, "previews");

        list.SelectedIndex = 0;
        list.SelectedIndex = 1;

        launcher.Processes[0].End(1);

        Assert.Equal("Showing Quiet", StatusOf(panel));
    }

    [Fact]
    public void The_Stop_button_ends_the_Preview_and_Reload_reads_the_assembly_again()
    {
        var launcher = new FakeLauncher("Primary");
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Light);

        Find<ListView>(panel, "previews").SelectedIndex = 0;

        Press(Find<Button>(panel, "stop-preview"));

        Assert.False(panel.IsRunning);
        Assert.Equal("Stopped", StatusOf(panel));

        Press(Find<Button>(panel, "reload-previews"));

        Assert.Equal("1 Preview(s)", StatusOf(panel));
    }

    [Fact]
    public void An_assembly_that_cannot_be_read_shows_the_reason()
    {
        var launcher = new FakeLauncher { Failure = "The assembly was not found." };
        var panel = new PreviewsPanel(launcher, "missing.dll", () => ThemeVariant.Light);

        Assert.Empty(panel.Names);
        Assert.Equal("The assembly was not found.", StatusOf(panel));
    }

    [Fact]
    public void The_exit_is_handed_to_the_post_function()
    {
        var launcher = new FakeLauncher("Primary");
        var posted = new List<Action>();
        var panel = new PreviewsPanel(launcher, "app.dll", () => ThemeVariant.Light, null, posted.Add);

        Find<ListView>(panel, "previews").SelectedIndex = 0;

        launcher.Processes[0].End(0);

        Assert.Equal("Showing Primary", StatusOf(panel));

        posted.Single()();

        Assert.Equal("Primary was closed", StatusOf(panel));
    }
}

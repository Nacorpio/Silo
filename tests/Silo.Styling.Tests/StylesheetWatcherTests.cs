using Silo.Styling;
using Xunit;

public class StylesheetWatcherTests
{
    static async Task<T> Next<T>(Action<Action<T>> subscribe, Action trigger)
    {
        var source = new TaskCompletionSource<T>();

        subscribe(value => source.TrySetResult(value));
        trigger();

        return await source.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Saving_the_file_raises_Reloaded_with_the_new_rules()
    {
        var dir = Directory.CreateTempSubdirectory();
        var file = Path.Combine(dir.FullName, "t.jsonc");

        File.WriteAllText(file, """{ "rules": { "A": { "opacity": "1" } } }""");

        using var watcher = new StylesheetWatcher(file) { Delay = TimeSpan.FromMilliseconds(20) };
        var edited = """{ "rules": { "A": { "opacity": "1" }, "B": { "opacity": "0" } } }""";

        var sheet = await Next<Stylesheet>(a => watcher.Reloaded += a.Invoke, () => File.WriteAllText(file, edited));

        Assert.Equal(2, sheet.Rules.Count);
        dir.Delete(true);
    }

    [Fact]
    public async Task A_broken_file_raises_Failed_and_a_fixed_one_reloads()
    {
        var dir = Directory.CreateTempSubdirectory();
        var file = Path.Combine(dir.FullName, "t.jsonc");

        File.WriteAllText(file, """{ "rules": {} }""");

        using var watcher = new StylesheetWatcher(file) { Delay = TimeSpan.FromMilliseconds(20) };
        var error = await Next<Exception>(a => watcher.Failed += a.Invoke, () => File.WriteAllText(file, "{ nope"));

        Assert.IsType<StylesheetException>(error);

        var fixedText = """{ "rules": { "A": { "opacity": "1" } } }""";

        var sheet = await Next<Stylesheet>(a => watcher.Reloaded += a.Invoke, () => File.WriteAllText(file, fixedText));

        Assert.Single(sheet.Rules);
        dir.Delete(true);
    }
}

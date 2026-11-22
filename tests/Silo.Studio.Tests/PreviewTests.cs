using System.Reflection;
using Silo;
using Silo.Studio;
using Xunit;

public class PreviewTests
{
    static PreviewCatalog Catalog() => new(Assembly.GetExecutingAssembly());

    [Fact]
    public void The_catalog_lists_the_static_parameterless_methods_marked_as_Previews_by_name()
    {
        var names = Catalog().Previews.Select(p => p.Name);

        Assert.Equal(["Broken", "Primary button", "Quiet", "Wrapped window"], names);
    }

    [Fact]
    public void A_Preview_without_a_name_is_listed_by_its_method_name()
    {
        Assert.Contains(Catalog().Previews, p => p.Name == "Quiet");
    }

    [Fact]
    public void Methods_that_cannot_be_Previews_are_left_out()
    {
        var names = Catalog().Previews.Select(p => p.Name);

        Assert.DoesNotContain("TakesArgument", names);
        Assert.DoesNotContain("ReturnsString", names);
    }

    [Fact]
    public void Create_runs_the_method_and_returns_its_Element()
    {
        var element = Catalog().Create("primary BUTTON");

        Assert.Equal("Save", Assert.IsType<Button>(element).Text);
    }

    [Fact]
    public void Create_reports_a_name_that_is_not_listed()
    {
        Assert.Throws<ArgumentException>(() => Catalog().Create("Missing"));
    }

    [Fact]
    public void Create_reports_an_exception_of_the_method_with_its_message()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Catalog().Create("Broken"));

        Assert.Contains("sample data is missing", exception.Message);
    }

    [Fact]
    public void Wrap_puts_an_Element_in_a_Window_and_keeps_a_Window_as_it_is()
    {
        var wrapped = PreviewHost.Wrap(new Button(), "Title");
        var window = new Window();

        Assert.Equal("Title", wrapped.Title);
        Assert.Single(wrapped.Children);
        Assert.Same(window, PreviewHost.Wrap(window, "Other"));
    }

    [Fact]
    public void Run_lists_the_Previews_of_an_assembly_file()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        var writer = new StringWriter();
        var previous = Console.Out;

        Console.SetOut(writer);

        try
        {
            Assert.Equal(0, PreviewMode.Run(path, list: true, null, null, null, ThemeVariant.Light));
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.Contains("Primary button", writer.ToString());
    }

    [Fact]
    public void Run_fails_for_an_assembly_that_does_not_exist()
    {
        var code = PreviewMode.Run("missing.dll", list: true, null, null, null, ThemeVariant.Light);

        Assert.Equal(PreviewMode.Failed, code);
    }

    [Fact]
    public void Run_draws_a_Preview_to_a_png_without_a_window()
    {
        var path = Assembly.GetExecutingAssembly().Location;
        var png = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");

        try
        {
            Assert.Equal(0, PreviewMode.Run(path, list: false, "Primary button", null, png, ThemeVariant.Dark));
            Assert.True(new FileInfo(png).Length > 0);
        }
        finally
        {
            File.Delete(png);
        }
    }
}

/// <summary>The Previews that the tests find.</summary>
static class PreviewFixtures
{
    [Preview("Primary button")]
    public static Element Primary() => new Button { Text = "Save" };

    [Preview]
    static Element Quiet() => new TextBlock { Text = "Hello" };

    [Preview("Wrapped window")]
    public static Element WrappedWindow() => new Window { Title = "Sample" };

    [Preview("Broken")]
    public static Element Broken() => throw new InvalidOperationException("The sample data is missing.");

    [Preview]
    public static Element TakesArgument(int count) => new Button();

    [Preview]
    public static string ReturnsString() => "text";
}

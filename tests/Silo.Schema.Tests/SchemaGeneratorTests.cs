using System.Text.Json;
using Silo;
using Silo.Schema;
using Xunit;

public class SchemaGeneratorTests
{
    const string UpdateVariable = "SILO_UPDATE_SCHEMA";

    static JsonElement RuleProperties() => JsonDocument.Parse(SchemaGenerator.Generate()).RootElement
        .GetProperty("$defs").GetProperty("rule").GetProperty("properties");

    static string SchemaPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Silo.slnx")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Silo.slnx not found."),
            "schema", "silo-stylesheet.schema.json");
    }

    [Fact]
    public void The_committed_schema_is_the_generated_one()
    {
        var path = SchemaPath();
        var generated = SchemaGenerator.Generate();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
            File.WriteAllText(path, generated);

        Assert.True(File.Exists(path), $"Run the tests once with {UpdateVariable}=1 to create {path}.");
        Assert.Equal(generated, File.ReadAllText(path).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Generating_twice_gives_the_same_text()
    {
        Assert.Equal(SchemaGenerator.Generate(), SchemaGenerator.Generate());
    }

    [Fact]
    public void Properties_of_every_Control_are_known_and_a_color_is_a_scalar()
    {
        var properties = RuleProperties();

        Assert.True(properties.TryGetProperty("background", out var background));
        Assert.True(properties.TryGetProperty("max-rows", out _));
        Assert.True(properties.TryGetProperty("indicator-mark-color", out _));
        Assert.Equal("#/$defs/scalar", background.GetProperty("$ref").GetString());
    }

    [Fact]
    public void A_keyword_property_lists_its_keywords_and_still_allows_a_Token_reference()
    {
        var direction = RuleProperties().GetProperty("flex-direction").GetProperty("anyOf");
        var keywords = direction[0].GetProperty("enum").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Equal(["row", "column"], keywords);
        Assert.Contains("var", direction[1].GetProperty("pattern").GetString());
    }

    [Fact]
    public void Every_property_that_the_default_theme_uses_is_in_the_schema()
    {
        var properties = RuleProperties();
        var sheet = DefaultTheme.Load();
        var used = sheet.Rules.SelectMany(r => r.Declarations).Select(d => d.Property).Distinct().ToList();

        Assert.NotEmpty(used);
        Assert.All(used, name => Assert.True(properties.TryGetProperty(name, out _), name));
    }
}

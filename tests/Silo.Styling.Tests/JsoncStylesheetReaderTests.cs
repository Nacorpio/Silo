using Silo.Styling;
using Xunit;

public class JsoncStylesheetReaderTests
{
    const string Source = """
    {
      // tokens cascade
      "tokens": { "--accent": "#3b82f6", },
      "rules": {
        "Button.primary": {
          "background": "var(--accent)",
          "&:hover": { "background": "red" },
          "&::part(icon)": { "margin-right": "6px" }
        },
        ".danger": { "--accent": "#dc2626" },
        "Button.primary": { "padding": "8 16" },
      }
    }
    """;

    [Fact]
    public void Reads_tokens_with_comments_and_trailing_commas()
    {
        var sheet = JsoncStylesheetReader.Read(Source, "t.jsonc");
        Assert.Equal("#3b82f6", sheet.Tokens["--accent"].Value);
    }

    [Fact]
    public void Flattens_nested_rules_in_source_order_and_keeps_duplicate_keys()
    {
        var sheet = JsoncStylesheetReader.Read(Source, "t.jsonc");
        Assert.Equal(
            ["Button.primary", "Button.primary:hover", "Button.primary::part(icon)", ".danger", "Button.primary"],
            sheet.Rules.Select(r => r.Selector.ToString()).ToArray());
    }

    [Fact]
    public void Separates_declarations_from_tokens_in_a_rule()
    {
        var sheet = JsoncStylesheetReader.Read(Source, "t.jsonc");
        var danger = sheet.Rules[3];
        Assert.Empty(danger.Declarations);
        Assert.Equal("#dc2626", danger.Tokens["--accent"].Value);
    }

    [Fact]
    public void Declarations_carry_source_spans()
    {
        var sheet = JsoncStylesheetReader.Read(Source, "t.jsonc");
        var decl = sheet.Rules[0].Declarations[0];

        Assert.Equal("background", decl.Property);
        Assert.Equal("var(--accent)", decl.Value);
        Assert.Equal("t.jsonc", decl.Span.File);
        Assert.Equal(6, decl.Span.Line);
    }

    [Fact]
    public void Throws_with_location_on_invalid_selector()
    {
        var ex = Assert.Throws<StylesheetException>(() =>
            JsoncStylesheetReader.Read("""{ "rules": { "Button..x": {} } }""", "bad.jsonc"));

        Assert.Contains("bad.jsonc", ex.Message);
    }
}

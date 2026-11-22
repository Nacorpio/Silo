using Silo;
using Silo.Styling;
using Silo.Validation;
using Xunit;

public class StylesheetValidatorTests
{
    static IReadOnlyList<ValidationIssue> Check(string json, params Stylesheet[] context)
        => StylesheetValidator.ValidateText(json, "t.jsonc", context);

    [Fact]
    public void The_default_theme_and_its_dark_variant_have_no_issues()
    {
        var light = DefaultTheme.Load();

        Assert.Empty(StylesheetValidator.Validate(light));
        Assert.Empty(StylesheetValidator.ValidateText(DefaultTheme.DarkSource(), DefaultTheme.DarkFileName, [light]));
    }

    [Fact]
    public void A_property_that_the_type_does_not_have_is_an_error_at_the_declaration()
    {
        var issues = Check("""
            {
              "rules": {
                "Button": {
                  "colour": "red"
                }
              }
            }
            """);

        var issue = Assert.Single(issues);

        Assert.Equal("SILO001", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal((4, 7), (issue.Span.Line, issue.Span.Column));
        Assert.Equal("t.jsonc(4,7): error SILO001: Button has no Styled property 'colour'.", issue.ToString());
    }

    [Fact]
    public void A_property_of_another_type_is_found_only_on_that_type()
    {
        Assert.Empty(Check("""{ "rules": { "Slider": { "slider-fill-color": "red" } } }"""));
        Assert.Equal("SILO001", Assert.Single(Check("""{ "rules": { "Box": { "slider-fill-color": "red" } } }""")).Code);
        Assert.Equal("SILO001", Assert.Single(Check("""{ "rules": { "Box": { "max-rows": "4" } } }""")).Code);
        Assert.Empty(Check("""{ "rules": { "ComboBox": { "max-rows": "4" } } }"""));
    }

    [Fact]
    public void A_value_that_the_property_does_not_accept_is_an_error_after_Tokens_are_replaced()
    {
        var issues = Check("""
            {
              "tokens": { "--dir": "diagonal" },
              "rules": { "Box": { "flex-direction": "var(--dir)", "gap": "wide", "padding": "4px" } }
            }
            """);

        Assert.Equal(["SILO002", "SILO002"], issues.Select(i => i.Code));
        Assert.Contains("'diagonal'", issues[0].Message);
        Assert.Contains("'wide'", issues[1].Message);
    }

    [Fact]
    public void A_Token_that_nothing_declares_is_an_error_but_one_from_the_context_is_fine()
    {
        const string Source = """{ "rules": { "Box": { "color": "var(--missing)" } } }""";

        var theme = JsoncStylesheetReader.Read("""{ "tokens": { "--missing": "red" } }""", "theme.jsonc");

        Assert.Equal("SILO003", Assert.Single(Check(Source)).Code);
        Assert.Empty(Check(Source, theme));
        Assert.Empty(Check("""{ "rules": { "Box": { "color": "var(--missing, red)" } } }"""));
    }

    [Fact]
    public void Tokens_that_refer_to_each_other_in_a_cycle_are_an_error()
    {
        var issues = Check("""{ "tokens": { "--a": "var(--b)", "--b": "var(--a)" } }""");

        Assert.All(issues, i => Assert.Equal("SILO003", i.Code));
        Assert.NotEmpty(issues);
    }

    [Fact]
    public void A_value_that_uses_a_Token_declared_by_a_Rule_is_not_checked_against_the_property()
    {
        var issues = Check("""
            {
              "rules": {
                ".wide": { "--size": "10px" },
                "Box": { "gap": "var(--size)" }
              }
            }
            """);

        Assert.Empty(issues);
    }

    [Fact]
    public void An_unknown_type_or_Pseudo_state_is_only_a_warning_and_the_property_is_still_looked_up()
    {
        var issues = Check("""{ "rules": { "MyCard:pressed": { "background": "red", "nonsense": "1" } } }""");

        Assert.Equal(["SILO004", "SILO005"], issues.Select(i => i.Code));
        Assert.All(issues, i => Assert.Equal(ValidationSeverity.Warning, i.Severity));
    }

    [Fact]
    public void A_Part_selector_and_the_wildcard_are_not_checked_for_property_names()
    {
        Assert.Empty(Check("""{ "rules": { "Button::part(icon)": { "anything": "1" }, "*": { "color": "red" } } }"""));
    }

    [Fact]
    public void Text_that_is_not_a_Stylesheet_gives_one_syntax_error_with_the_position()
    {
        var issue = Assert.Single(Check("""{ "rulez": {} }"""));

        Assert.Equal("SILO000", issue.Code);
        Assert.Equal(("t.jsonc", 1), (issue.Span.File, issue.Span.Line));
        Assert.Contains("Unknown top-level key 'rulez'", issue.Message);
    }
}

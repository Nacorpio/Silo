using Silo.Styling;
using Xunit;

public class JsoncDocumentTests
{
    const string Source = """
        {
          // Colors.
          "tokens": { "--accent": "#3b82f6", },
          "rules": {
            "Button": {
              "background": "var(--accent)", // the fill
              "padding": "6px 12px",
              "&:hover": { "background": "red" },
            },

            /* Plain text. */
            "TextBlock": { "color": "black" },
            "Box": {},
          },
        }
        """;

    static JsoncDocument Parse(string text = Source) => JsoncDocument.Parse(text, "t.jsonc");

    [Fact]
    public void The_text_is_kept_exactly_as_written()
    {
        var document = Parse();

        Assert.Equal(Source, document.Text);
        Assert.Equal(Source, document.ToString());
    }

    [Fact]
    public void Members_keep_their_order_keys_and_values_and_a_repeated_key_is_two_members()
    {
        var document = Parse("""{ "rules": { "A": { "gap": "1px" }, "A": { "gap": "2px" } } }""");
        var rules = (JsoncObject)document.Root.Members[0].Value;

        Assert.Equal(["A", "A"], rules.Members.Select(m => m.Key));
        Assert.Equal("2px", ((JsoncScalar)((JsoncObject)document.Find("rules", "A")!.Value).Members[0].Value).Value);
    }

    [Fact]
    public void Find_follows_a_path_and_returns_null_where_it_ends()
    {
        var document = Parse();

        Assert.Equal("6px 12px", ((JsoncScalar)document.Find("rules", "Button", "padding")!.Value).Value);
        Assert.Null(document.Find("rules", "Button", "margin"));
        Assert.Null(document.Find("rules", "Button", "padding", "deeper"));
        Assert.Null(document.Find("nothing"));
    }

    [Fact]
    public void Scalars_know_their_kind_and_unescape_strings()
    {
        var document = Parse("{ \"a\": \"x\\\"\\n\\u0041\", \"b\": 12, \"c\": true, \"d\": null }");
        var values = document.Root.Members.Select(m => (JsoncScalar)m.Value).ToList();

        Assert.Equal("x\"\nA", values[0].Value);
        Assert.Equal("\"x\\\"\\n\\u0041\"", values[0].Text);
        Assert.Equal([JsoncScalarKind.String, JsoncScalarKind.Number, JsoncScalarKind.Boolean, JsoncScalarKind.Null],
            values.Select(v => v.Kind));
    }

    [Theory]
    [InlineData("""{ "a": [1] }""", "t.jsonc(1,8): Arrays")]
    [InlineData("""{ "a": 1 """, "t.jsonc(1,10): Expected ',' or '}'")]
    [InlineData("""{ "a" 1 }""", "t.jsonc(1,7): Expected ':'")]
    [InlineData("""{ "a": "x }""", "t.jsonc(1,8): The string is not closed")]
    [InlineData("""{ } extra""", "t.jsonc(1,5): Unexpected text")]
    [InlineData("[]", "t.jsonc(1,1): Expected '{'")]
    [InlineData("""{ "a": nope }""", "t.jsonc(1,8): 'nope' is not a valid value")]
    [InlineData("{ /* open", "t.jsonc(1,3): The comment is not closed")]
    public void Invalid_text_is_reported_with_its_position(string text, string expectedStart)
    {
        var error = Assert.Throws<StylesheetException>(() => Parse(text));

        Assert.StartsWith(expectedStart, error.Message);
    }

    [Fact]
    public void PositionOf_gives_line_and_column()
    {
        var document = Parse();
        var member = document.Find("rules", "Button", "padding")!;

        Assert.Equal((7, 7), (document.PositionOf(member.Start).Line, document.PositionOf(member.Start).Column));
    }

    [Fact]
    public void Setting_an_existing_value_changes_only_that_value()
    {
        var edited = Parse().SetValue(["rules", "Button", "padding"], "8px 16px");

        Assert.Equal(Source.Replace("\"6px 12px\"", "\"8px 16px\""), edited.Text);
    }

    [Fact]
    public void An_edit_gives_a_new_document_and_leaves_the_old_one_as_it_was()
    {
        var original = Parse();
        var edited = original.SetValue(["tokens", "--accent"], "#000");

        Assert.Equal(Source, original.Text);
        Assert.NotEqual(Source, edited.Text);
    }

    [Fact]
    public void A_member_added_to_a_multi_line_object_with_a_trailing_comma_follows_the_last_one_with_a_comma()
    {
        var edited = Parse().SetValue(["rules", "TextBlock", "font-size"], "12px");

        Assert.Contains("\"TextBlock\": { \"color\": \"black\", \"font-size\": \"12px\" },", edited.Text);
    }

    [Fact]
    public void A_member_added_after_one_with_a_trailing_comment_goes_below_the_comment_with_the_same_indent()
    {
        const string Text = """
            {
              "rules": {
                "Button": {
                  "background": "red", // the fill
                  "color": "white"
                }
              }
            }
            """;

        var edited = Parse(Text).SetValue(["rules", "Button", "gap"], "4px");

        Assert.Equal("""
            {
              "rules": {
                "Button": {
                  "background": "red", // the fill
                  "color": "white",
                  "gap": "4px"
                }
              }
            }
            """, edited.Text);
    }

    [Fact]
    public void A_trailing_comma_is_kept_as_a_style_when_a_member_is_added()
    {
        const string Text = "{\n  \"rules\": {\n    \"A\": {\n      \"gap\": \"1px\", // one\n    },\n  },\n}";

        var edited = Parse(Text).SetValue(["rules", "A", "color"], "red");

        const string Expected = "{\n  \"rules\": {\n    \"A\": {\n      \"gap\": \"1px\", // one\n      \"color\": \"red\",\n    },\n  },\n}";

        Assert.Equal(Expected, edited.Text);
    }

    [Fact]
    public void A_member_added_to_an_empty_object_goes_inside_it()
    {
        var edited = Parse().SetValue(["rules", "Box", "gap"], "4px");

        Assert.Contains("\"Box\": { \"gap\": \"4px\" },", edited.Text);
    }

    [Fact]
    public void A_member_added_to_an_empty_multi_line_object_is_indented_one_level_deeper_than_its_brace()
    {
        var edited = Parse("{\n  \"rules\": {\n  }\n}").SetValue(["rules", "Box", "gap"], "4px");

        Assert.Equal("{\n  \"rules\": {\n    \"Box\": {\n      \"gap\": \"4px\"\n    }\n  }\n}", edited.Text);
    }

    [Fact]
    public void Missing_objects_on_the_path_are_created()
    {
        var edited = Parse("{}").SetValue(["rules", "Button", "&:hover", "background"], "red");

        Assert.Equal("{ \"rules\": { \"Button\": { \"&:hover\": { \"background\": \"red\" } } } }", edited.Text);

        var sheet = edited.ToStylesheet();

        Assert.Equal(["Button", "Button:hover"], sheet.Rules.Select(r => r.Selector.ToString()));
    }

    [Fact]
    public void An_edit_can_be_read_back_as_a_Stylesheet()
    {
        var sheet = Parse().SetValue(["rules", "Button", "padding"], "1px").ToStylesheet();
        var button = sheet.Rules.First(r => r.Selector.ToString() == "Button");

        Assert.Equal("1px", button.Declarations.Single(d => d.Property == "padding").Value);
    }

    [Fact]
    public void A_raw_value_is_written_as_given_and_a_string_is_quoted_and_escaped()
    {
        var number = Parse("""{ "a": { "n": 1 } }""").SetRawValue(["a", "n"], "2");
        var quoted = Parse("""{ "a": { "s": "" } }""").SetValue(["a", "s"], "say \"hi\"\n");

        Assert.Equal("""{ "a": { "n": 2 } }""", number.Text);
        Assert.Equal(@"say ""hi""" + "\n", ((JsoncScalar)quoted.Find("a", "s")!.Value).Value);
    }

    [Fact]
    public void A_value_cannot_replace_an_object_or_go_inside_a_value()
    {
        var document = Parse();

        Assert.Throws<InvalidOperationException>(() => document.SetValue(["rules", "Button"], "x"));
        Assert.Throws<InvalidOperationException>(() => document.SetValue(["rules", "Button", "padding", "x"], "x"));
        Assert.Throws<ArgumentException>(() => document.SetValue([], "x"));
    }

    [Fact]
    public void Removing_a_member_that_is_alone_on_its_line_removes_the_line_and_its_comment()
    {
        var edited = Parse().Remove(["rules", "Button", "background"]);

        Assert.DoesNotContain("the fill", edited.Text);
        Assert.DoesNotContain("\"background\": \"var(--accent)\"", edited.Text);
        Assert.Contains("\"padding\": \"6px 12px\",", edited.Text);
        Assert.Contains("// Colors.", edited.Text);
    }

    [Fact]
    public void Removing_inline_members_takes_the_comma_with_them()
    {
        var first = Parse("""{ "a": 1, "b": 2, "c": 3 }""").Remove(["a"]);
        var last = Parse("""{ "a": 1, "b": 2, "c": 3 }""").Remove(["c"]);

        Assert.Equal("""{ "b": 2, "c": 3 }""", first.Text);
        Assert.Equal("""{ "a": 1, "b": 2 }""", last.Text);
    }

    [Fact]
    public void Removing_a_missing_member_gives_the_same_document()
    {
        var document = Parse();

        Assert.Same(document, document.Remove(["rules", "Nothing"]));
    }

    [Fact]
    public void Comments_between_members_survive_edits_elsewhere()
    {
        var edited = Parse()
            .SetValue(["rules", "TextBlock", "color"], "white")
            .Remove(["rules", "Button", "&:hover"]);

        Assert.Contains("/* Plain text. */", edited.Text);
        Assert.Contains("// Colors.", edited.Text);
        Assert.Contains("// the fill", edited.Text);
    }

    [Fact]
    public void A_document_with_Windows_line_breaks_gets_Windows_line_breaks_in_added_members()
    {
        var edited = Parse("{\r\n  \"rules\": {\r\n    \"A\": {\r\n      \"gap\": \"1px\"\r\n    }\r\n  }\r\n}")
            .SetValue(["rules", "A", "color"], "red");

        const string Expected = "{\r\n  \"rules\": {\r\n    \"A\": {\r\n      \"gap\": \"1px\",\r\n      \"color\": \"red\"\r\n    }\r\n  }\r\n}";

        Assert.Equal(Expected, edited.Text);
    }
}

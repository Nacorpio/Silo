using Silo;
using Xunit;

public class EmojiCatalogTests
{
    [Fact]
    public void The_catalog_has_the_groups_of_unicode_and_over_a_thousand_emoji()
    {
        Assert.True(EmojiCatalog.All.Count > 1000);
        Assert.Equal(9, EmojiCatalog.Groups.Count);
        Assert.Equal("Smileys & Emotion", EmojiCatalog.Groups[0]);
        Assert.Equal("Flags", EmojiCatalog.Groups[^1]);
    }

    [Fact]
    public void An_emoji_with_tones_has_five_variants_and_other_emoji_have_none()
    {
        var wave = EmojiCatalog.Get("\U0001F44B")!;
        var grin = EmojiCatalog.Get("\U0001F600")!;

        Assert.Equal(5, wave.Tones.Count);
        Assert.Equal("\U0001F44B\U0001F3FB", wave.In(SkinTone.Light));
        Assert.Equal("\U0001F44B\U0001F3FF", wave.In(SkinTone.Dark));
        Assert.Equal(wave.Value, wave.In(SkinTone.Default));
        Assert.Empty(grin.Tones);
        Assert.Equal(grin.Value, grin.In(SkinTone.Dark));
    }

    [Fact]
    public void A_toned_emoji_is_found_by_its_base_entry()
    {
        Assert.Equal("waving hand", EmojiCatalog.Get("\U0001F44B\U0001F3FD")!.Name);
        Assert.Null(EmojiCatalog.Get("not an emoji"));
    }

    [Fact]
    public void Gender_and_family_sequences_are_left_out_but_the_neutral_person_stays()
    {
        Assert.DoesNotContain(EmojiCatalog.All, e => e.Name.StartsWith("man technologist") || e.Name.StartsWith("woman technologist"));
        Assert.Contains(EmojiCatalog.All, e => e.Name == "technologist");
        Assert.DoesNotContain(EmojiCatalog.All, e => e.Name.StartsWith("family: man") || e.Name.StartsWith("family: woman"));
        Assert.Contains(EmojiCatalog.All, e => e.Name == "family: adult, adult, child");
    }

    [Fact]
    public void Search_matches_names_and_keywords_and_ranks_name_starts_first()
    {
        var found = EmojiCatalog.Find("grin");

        Assert.NotEmpty(found);
        Assert.StartsWith("grin", found[0].Name);
        Assert.Contains(EmojiCatalog.Find("lol"), e => e.Name == "grinning face with smiling eyes" || e.Keywords.Contains("lol"));
    }

    [Fact]
    public void Search_ignores_case_a_leading_colon_and_matches_every_word()
    {
        Assert.Equal(EmojiCatalog.Find("smile").Select(e => e.Value), EmojiCatalog.Find(":SMILE").Select(e => e.Value));

        var both = EmojiCatalog.Find("face tears");

        Assert.NotEmpty(both);
        Assert.All(both, e => Assert.True(e.Name.Contains("tear") || e.Keywords.Any(k => k.StartsWith("tear"))));
    }

    [Fact]
    public void An_empty_or_unmatched_query_finds_nothing()
    {
        Assert.Empty(EmojiCatalog.Find(string.Empty));
        Assert.Empty(EmojiCatalog.Find("  "));
        Assert.Empty(EmojiCatalog.Find("zzzzzz"));
    }

    [Fact]
    public void Flags_are_found_by_country_name_although_they_have_no_keywords()
    {
        Assert.Contains(EmojiCatalog.Find("sweden"), e => e.Value == "\U0001F1F8\U0001F1EA");
    }
}

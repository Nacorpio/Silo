using Silo.Layout;

namespace Silo;

/// <summary>
/// An inline emoji picker: a search box, a button for the skin tone, a strip of group icons and an <see cref="EmojiGrid"/>.
/// </summary>
/// <remarks>
/// <para>
/// Typing goes to the search box, which matches names and keywords. The arrows, Page Up, Page Down, Home, End and Enter work on the
/// grid, so a search can be typed and then walked without moving the focus. The group icons scroll the grid to that group and show
/// which group is in view. The tone button steps through <see cref="SkinTone"/>: it applies to every emoji that has tones.
/// </para>
/// <para>
/// The panel keeps the <see cref="Recent"/> emoji in memory, newest first, at most <see cref="MaxRecent"/> of them, and raises
/// <see cref="RecentChanged"/> so that the app can save them. It never touches the disk. After a pick, <see cref="Submitted"/> is
/// raised when <see cref="CloseOnPick"/> is set, which is what an <see cref="EmojiPicker"/> closes its popup on.
/// </para>
/// <para>
/// The look comes from the theme through Classes on the parts: <c>search</c>, <c>tone</c>, <c>categories</c>, <c>category</c> (and
/// <c>current</c> on the one in view) and <c>bar</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var panel = new EmojiPanel { Recent = settings.RecentEmoji };
///
/// panel.EmojiPicked += (_, emoji) => composer.Insert(emoji);
/// panel.RecentChanged += (_, _) => settings.RecentEmoji = [.. panel.Recent];
/// </code>
/// </example>
public class EmojiPanel : Control
{
    /// <summary>The title of the section with the recent emoji.</summary>
    internal const string RecentTitle = "Recent";

    /// <summary>The icons of the group buttons.</summary>
    static readonly Dictionary<string, string> GroupIcons = new()
    {
        [RecentTitle] = "\U0001F552",
        ["Smileys & Emotion"] = "\U0001F600",
        ["People & Body"] = "\U0001F44B",
        ["Animals & Nature"] = "\U0001F43B",
        ["Food & Drink"] = "\U0001F354",
        ["Travel & Places"] = "\U0001F697",
        ["Activities"] = "⚽",
        ["Objects"] = "\U0001F4A1",
        ["Symbols"] = "\U0001F523",
        ["Flags"] = "\U0001F3C1",
    };

    /// <summary>The most emoji that <see cref="Recent"/> holds.</summary>
    public const int MaxRecent = 24;


    /// <summary>The search box.</summary>
    readonly SearchBox search = new();

    /// <summary>The button that steps through the skin tones.</summary>
    readonly Button toneButton = new();

    /// <summary>The row of group buttons.</summary>
    readonly Box categories = new();

    /// <summary>The emoji.</summary>
    readonly EmojiGrid grid = new();

    /// <summary>The recent emoji, newest first.</summary>
    readonly List<string> recent = [];

    /// <summary>The titles that the group buttons were made for.</summary>
    List<string> buttonTitles = [];

    /// <summary>Whether a pick raises <see cref="Submitted"/>.</summary>
    bool closeOnPick = true;


    /// <summary>Creates a panel with every group and no recent emoji.</summary>
    public EmojiPanel()
    {
        var bar = new Box();

        bar.AddClass("bar");
        search.AddClass("search");
        toneButton.AddClass("tone");
        categories.AddClass("categories");

        search.TextChanged += (_, _) => OnSearchChanged();
        toneButton.Click += (_, _) => StepTone();
        grid.EmojiPicked += (_, emoji) => OnPicked(emoji);
        grid.CurrentSectionChanged += (_, _) => HighlightCurrent();

        bar.Children.Add(search);
        bar.Children.Add(toneButton);

        Children.Add(bar);
        Children.Add(categories);
        Children.Add(grid);

        RefreshToneButton();
        RebuildCategories();
    }


    /// <summary>Occurs when an emoji is picked. The argument is the emoji, in the current skin tone.</summary>
    public event EventHandler<string>? EmojiPicked;

    /// <summary>Occurs when <see cref="Recent"/> changed because of a pick.</summary>
    public event EventHandler? RecentChanged;

    /// <summary>Occurs when the skin tone was stepped with the tone button.</summary>
    public event EventHandler? SkinToneChanged;

    /// <summary>Occurs after a pick when <see cref="CloseOnPick"/> is set. An <see cref="EmojiPicker"/> closes its popup on it.</summary>
    public event EventHandler? Submitted;


    /// <summary>Gets or sets whether a pick raises <see cref="Submitted"/>. The default is <see langword="true"/>; set it to <see langword="false"/> to pick several in a row, as a chat does for reactions.</summary>
    public bool CloseOnPick
    {
        get => closeOnPick;
        set => closeOnPick = value;
    }

    /// <summary>Gets or sets the skin tone of the emoji that have tones.</summary>
    public SkinTone SkinTone
    {
        get => grid.SkinTone;
        set
        {
            grid.SkinTone = value;

            RefreshToneButton();
        }
    }

    /// <summary>Gets or sets the recent emoji, newest first. A list longer than <see cref="MaxRecent"/> is cut.</summary>
    public IReadOnlyList<string> Recent
    {
        get => recent;
        set
        {
            recent.Clear();
            recent.AddRange((value ?? []).Distinct().Take(MaxRecent));

            ApplyRecent();
        }
    }

    /// <summary>Gets the grid, for the cell size and the fonts.</summary>
    public EmojiGrid Grid => grid;

    /// <summary>Gets the search box.</summary>
    public SearchBox SearchBox => search;

    /// <summary>Gets or sets the search text.</summary>
    public string Query
    {
        get => search.Text;
        set => search.Text = value;
    }

    /// <summary>Puts the keyboard on the search box, so that typing searches. Call it when the panel opens.</summary>
    public void BeginKeyboard() => search.SetFocused(true);

    /// <inheritdoc/>
    public override bool KeyDown(Key key) => KeyDown(key, KeyModifiers.None);

    /// <inheritdoc/>
    public override bool KeyDown(Key key, KeyModifiers modifiers)
    {
        if (modifiers == KeyModifiers.None && key is Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Enter)
            return grid.KeyDown(key, modifiers);

        return search.KeyDown(key, modifiers);
    }

    /// <inheritdoc/>
    public override bool TextInput(string text) => search.TextInput(text);

    /// <summary>Raises the events of a pick and keeps the recent list.</summary>
    /// <param name="emoji">The emoji that was picked.</param>
    void OnPicked(string emoji)
    {
        recent.Remove(emoji);
        recent.Insert(0, emoji);

        if (recent.Count > MaxRecent)
            recent.RemoveRange(MaxRecent, recent.Count - MaxRecent);

        EmojiPicked?.Invoke(this, emoji);
        RecentChanged?.Invoke(this, EventArgs.Empty);

        // The grid keeps showing what it showed while the panel stays open, so the list under the pointer does not jump.
        if (closeOnPick)
            Submitted?.Invoke(this, EventArgs.Empty);
        else
            ApplyRecent();
    }

    /// <summary>Gives the grid the recent list and rebuilds the group buttons when the Recent button comes or goes.</summary>
    void ApplyRecent()
    {
        grid.Recent = [.. recent];

        RebuildCategories();
    }

    /// <summary>Searches while typing and shows the group strip only when there is no search.</summary>
    void OnSearchChanged()
    {
        grid.Query = search.Text;

        categories.SetValue(FlexShrinkProperty, search.Text.Length > 0 ? 1 : 0);

        HighlightCurrent();
    }

    /// <summary>Steps to the next skin tone and back to the default after the last.</summary>
    void StepTone()
    {
        SkinTone = (SkinTone)(((int)SkinTone + 1) % (Enum.GetValues<SkinTone>().Length));

        SkinToneChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the current skin tone on the tone button as a waving hand.</summary>
    void RefreshToneButton()
    {
        var tone = (int)grid.SkinTone;

        toneButton.Text = tone == 0 ? "\U0001F44B" : "\U0001F44B" + char.ConvertFromUtf32(0x1F3FA + tone);
    }

    /// <summary>Makes a button for each section that the grid shows.</summary>
    void RebuildCategories()
    {
        var titles = EmojiCatalog.Groups.Prepend(recent.Count > 0 ? RecentTitle : string.Empty).Where(t => t.Length > 0).ToList();

        if (titles.SequenceEqual(buttonTitles))
        {
            HighlightCurrent();

            return;
        }

        buttonTitles = titles;
        categories.Children.Clear();

        for (var i = 0; i < titles.Count; i++)
        {
            var index = i;
            var button = new Button { Text = GroupIcons.GetValueOrDefault(titles[i], "\U0001F4C1") };

            button.AddClass("category");
            button.Click += (_, _) => JumpTo(index);

            categories.Children.Add(button);
        }

        HighlightCurrent();
    }

    /// <summary>Scrolls the grid to a section, ending a search first.</summary>
    /// <param name="index">The index of the group button.</param>
    void JumpTo(int index)
    {
        if (search.Text.Length > 0)
            search.Text = string.Empty;

        grid.ScrollToSection(index);

        HighlightCurrent();
    }

    /// <summary>Marks the button of the section in view.</summary>
    void HighlightCurrent()
    {
        var current = grid.CurrentSection;

        for (var i = 0; i < categories.Children.Count; i++)
        {
            if (current == i)
                categories.Children[i].AddClass("current");
            else
                categories.Children[i].RemoveClass("current");
        }
    }
}

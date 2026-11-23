namespace Silo;

/// <summary>A row of page numbers with buttons for the previous and the next page.</summary>
/// <remarks>
/// <para>
/// The Pager always shows the first and last page and the pages next to the current one; where pages are left out it shows a
/// TextBlock with the Class <c>ellipsis</c>. A page that would be the only one left out is shown instead of the ellipsis. The
/// buttons have the Classes <c>prev</c>, <c>next</c> and <c>page</c>, and the button of the current page also <c>current</c>.
/// A previous or next button that cannot move has the Class <c>disabled</c>.
/// </para>
/// <para>
/// The Pager takes the keyboard focus. Left and Right change the page by one, Home and End go to the first and the last. Pages
/// count from 1. Apply styles again after any change.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var pager = new Pager { PageCount = 20, CurrentPage = 3 };
///
/// pager.PageChanged += (_, _) => grid.Load(pager.CurrentPage);
/// </code>
/// </example>
public class Pager : Control
{
    /// <summary>The number of pages.</summary>
    int pageCount = 1;

    /// <summary>The current page, from 1.</summary>
    int currentPage = 1;


    /// <summary>
    /// Runs before the first Pager is created, so that the Styled properties are registered in time.
    /// See the remarks on <see cref="Element"/>.
    /// </summary>
    static Pager()
    {
    }

    /// <summary>Creates a Pager with one page.</summary>
    public Pager() => Rebuild();

    /// <summary>Raised after <see cref="CurrentPage"/> changed.</summary>
    public event EventHandler? PageChanged;

    /// <summary>Gets or sets the number of pages. At least 1; the current page is kept in range.</summary>
    public int PageCount
    {
        get => pageCount;
        set
        {
            pageCount = Math.Max(1, value);

            CurrentPage = currentPage;

            Rebuild();
        }
    }

    /// <summary>Gets or sets the current page, from 1 to <see cref="PageCount"/>. Other values are moved into that range.</summary>
    public int CurrentPage
    {
        get => currentPage;
        set
        {
            var next = Math.Clamp(value, 1, pageCount);

            if (next == currentPage)
                return;

            currentPage = next;

            Rebuild();

            PageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        var before = currentPage;

        switch (key)
        {
            case Key.Left:
                CurrentPage--;
                break;

            case Key.Right:
                CurrentPage++;
                break;

            case Key.Home:
                CurrentPage = 1;
                break;

            case Key.End:
                CurrentPage = pageCount;
                break;
        }

        return before != currentPage;
    }

    /// <summary>Finds the pages that are shown, with 0 for an ellipsis.</summary>
    /// <returns>The page numbers in order.</returns>
    List<int> VisiblePages()
    {
        var wanted = new SortedSet<int> { 1, pageCount, currentPage - 1, currentPage, currentPage + 1 };

        wanted.RemoveWhere(page => page < 1 || page > pageCount);

        var result = new List<int>();
        var previous = 0;

        foreach (var page in wanted)
        {
            if (page - previous == 2)
                result.Add(previous + 1);
            else if (page - previous > 2)
                result.Add(0);

            result.Add(page);

            previous = page;
        }

        return result;
    }

    /// <summary>Builds the buttons.</summary>
    void Rebuild()
    {
        Children.Clear();

        Children.Add(Step("‹", "prev", currentPage > 1, -1));

        foreach (var page in VisiblePages())
        {
            if (page == 0)
            {
                var ellipsis = new TextBlock { Text = "…" };

                ellipsis.AddClass("ellipsis");
                Children.Add(ellipsis);

                continue;
            }

            var button = new Button { Text = page.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            var target = page;

            button.AddClass("page");

            if (page == currentPage)
                button.AddClass("current");

            button.Click += (_, _) => CurrentPage = target;

            Children.Add(button);
        }

        Children.Add(Step("›", "next", currentPage < pageCount, 1));
    }

    /// <summary>Creates the previous or next button.</summary>
    /// <param name="text">The arrow.</param>
    /// <param name="className">The Class of the button.</param>
    /// <param name="enabled">Whether the button can move.</param>
    /// <param name="delta">How far it moves the page.</param>
    /// <returns>The button.</returns>
    Button Step(string text, string className, bool enabled, int delta)
    {
        var button = new Button { Text = text };

        button.AddClass(className);

        if (!enabled)
            button.AddClass("disabled");

        button.Click += (_, _) => CurrentPage += delta;

        return button;
    }
}

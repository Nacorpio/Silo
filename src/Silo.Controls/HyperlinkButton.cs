namespace Silo;

/// <summary>A button that looks like a link and raises <see cref="Navigate"/> when it is clicked.</summary>
/// <remarks>
/// <para>
/// A HyperlinkButton does not open anything itself: the application handles <see cref="Navigate"/> and opens the
/// <see cref="Uri"/> in a browser, or goes to a page of its own. It is a <see cref="Button"/>, so it also raises
/// <see cref="Button.Click"/> and takes the <c>hover</c> and <c>active</c> Pseudo-states.
/// </para>
/// <para>
/// While <see cref="IsVisited"/> is set the HyperlinkButton has the <c>visited</c> Pseudo-state, so a Stylesheet can restyle it
/// with <c>HyperlinkButton:visited</c>. The application sets it, for example in the <see cref="Navigate"/> handler.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var docs = new HyperlinkButton { Text = "Documentation", Uri = "https://example.com/docs" };
///
/// docs.Navigate += (_, e) =>
/// {
///     Launcher.Open(e.Uri);
///     docs.IsVisited = true;
/// };
/// </code>
/// </example>
[PseudoStates("hover", "active", "focus", "visited")]
public class HyperlinkButton : Button
{
    /// <summary>Whether the link was followed.</summary>
    bool isVisited;


    /// <summary>Raised when the HyperlinkButton is clicked, with the <see cref="Uri"/>.</summary>
    public event EventHandler<NavigateEventArgs>? Navigate;


    /// <summary>Gets or sets where the link goes. The application interprets it.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the link was followed, which sets the <c>visited</c> Pseudo-state. Styles must be applied again after it changes.</summary>
    public bool IsVisited
    {
        get => isVisited;
        set
        {
            isVisited = value;

            SetPseudoState("visited", value);
        }
    }

    /// <inheritdoc/>
    protected override bool OnClick()
    {
        Navigate?.Invoke(this, new NavigateEventArgs(Uri));

        return false;
    }
}

/// <summary>The data of <see cref="HyperlinkButton.Navigate"/>.</summary>
/// <param name="uri">Where the link goes.</param>
public sealed class NavigateEventArgs(string uri) : EventArgs
{
    /// <summary>Gets where the link goes.</summary>
    public string Uri { get; } = uri;
}

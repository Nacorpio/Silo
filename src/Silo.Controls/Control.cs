using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;

namespace Silo;

/// <summary>
/// An Element that takes part in layout and paints itself: the base class of Box, TextBlock, Button and every other Control.
/// </summary>
/// <remarks>
/// <para>
/// A Control declares the Styled properties that Stylesheets use for layout (<c>width</c>, <c>padding</c>,
/// <c>flex-direction</c> and so on) and for painting (<c>background</c>, <c>border-color</c>, <c>border-radius</c>,
/// <c>color</c>, <c>font-size</c>). It implements <see cref="ILayoutNode"/> by reading those properties, so
/// <see cref="Arrange"/> lays out the whole tree with the flexbox engine.
/// </para>
/// <para>
/// The frame loop is: <see cref="Element.ApplyStyles"/>, then <see cref="Arrange"/>, then <see cref="Render"/>. Every child of
/// a Control must be a Control.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var root = new Box { Name = "root" };
///
/// root.Children.Add(new TextBlock { Text = "Hello" });
///
/// root.TextMeasurer = new SkiaTextMeasurer();
/// root.ApplyStyles(new StyleResolver([theme]));
/// root.Arrange(new Size(400, 300));
///
/// var frame = new DisplayListBuilder();
///
/// root.Render(frame);
/// </code>
/// </example>
public abstract class Control : Element, ILayoutNode
{
    /// <summary>The width, or <see langword="null"/> for <c>auto</c>.</summary>
    public static readonly StyledProperty<Length?> WidthProperty =
        StyledProperty.Register<Control, Length?>("width", null, ValueParsers.AutoLength);

    /// <summary>The height, or <see langword="null"/> for <c>auto</c>.</summary>
    public static readonly StyledProperty<Length?> HeightProperty =
        StyledProperty.Register<Control, Length?>("height", null, ValueParsers.AutoLength);

    /// <summary>The smallest width, or <see langword="null"/> for none.</summary>
    public static readonly StyledProperty<Length?> MinWidthProperty =
        StyledProperty.Register<Control, Length?>("min-width", null, ValueParsers.AutoLength);

    /// <summary>The largest width, or <see langword="null"/> for none.</summary>
    public static readonly StyledProperty<Length?> MaxWidthProperty =
        StyledProperty.Register<Control, Length?>("max-width", null, ValueParsers.AutoLength);

    /// <summary>The smallest height, or <see langword="null"/> for none.</summary>
    public static readonly StyledProperty<Length?> MinHeightProperty =
        StyledProperty.Register<Control, Length?>("min-height", null, ValueParsers.AutoLength);

    /// <summary>The largest height, or <see langword="null"/> for none.</summary>
    public static readonly StyledProperty<Length?> MaxHeightProperty =
        StyledProperty.Register<Control, Length?>("max-height", null, ValueParsers.AutoLength);

    /// <summary>The starting main size inside a flex parent, or <see langword="null"/> for <c>auto</c>.</summary>
    public static readonly StyledProperty<Length?> FlexBasisProperty =
        StyledProperty.Register<Control, Length?>("flex-basis", null, ValueParsers.AutoLength);

    /// <summary>The share of free space this Control takes in its parent.</summary>
    public static readonly StyledProperty<double> FlexGrowProperty =
        StyledProperty.Register<Control, double>("flex-grow", 0, ValueParsers.Number);

    /// <summary>How strongly this Control gives up space when its parent overflows.</summary>
    public static readonly StyledProperty<double> FlexShrinkProperty =
        StyledProperty.Register<Control, double>("flex-shrink", 1, ValueParsers.Number);

    /// <summary>The main axis of this Control's children.</summary>
    public static readonly StyledProperty<FlexDirection> FlexDirectionProperty =
        StyledProperty.Register<Control, FlexDirection>("flex-direction", FlexDirection.Row, ValueParsers.Direction);

    /// <summary>How free main-axis space is distributed between the children.</summary>
    public static readonly StyledProperty<JustifyContent> JustifyContentProperty =
        StyledProperty.Register<Control, JustifyContent>("justify-content", JustifyContent.Start, ValueParsers.Justify);

    /// <summary>The default cross-axis alignment of the children.</summary>
    public static readonly StyledProperty<AlignItems> AlignItemsProperty =
        StyledProperty.Register<Control, AlignItems>("align-items", AlignItems.Stretch, ValueParsers.Align);

    /// <summary>This Control's own cross-axis alignment, or <see langword="null"/> to use the parent's <c>align-items</c>.</summary>
    public static readonly StyledProperty<AlignItems?> AlignSelfProperty =
        StyledProperty.Register<Control, AlignItems?>("align-self", null, ValueParsers.AlignSelf);

    /// <summary>The space between adjacent children on the main axis.</summary>
    public static readonly StyledProperty<Length> GapProperty =
        StyledProperty.Register<Control, Length>("gap", default, ValueParsers.RequiredLength);

    /// <summary>The space between the border and the children.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        StyledProperty.Register<Control, Thickness>("padding", default, Thickness.TryParse);

    /// <summary>The space around the Control, outside its border.</summary>
    public static readonly StyledProperty<Thickness> MarginProperty =
        StyledProperty.Register<Control, Thickness>("margin", default, Thickness.TryParse);

    /// <summary>The width of the border on each side.</summary>
    public static readonly StyledProperty<Thickness> BorderWidthProperty =
        StyledProperty.Register<Control, Thickness>("border-width", default, Thickness.TryParse);

    /// <summary>The fill color of the border box. Fully transparent by default.</summary>
    public static readonly StyledProperty<Color> BackgroundProperty =
        StyledProperty.Register<Control, Color>("background", new Color(0, 0, 0, 0), Color.TryParse);

    /// <summary>The color of the border. Fully transparent by default.</summary>
    public static readonly StyledProperty<Color> BorderColorProperty =
        StyledProperty.Register<Control, Color>("border-color", new Color(0, 0, 0, 0), Color.TryParse);

    /// <summary>The corner radius of the background and border in pixels.</summary>
    public static readonly StyledProperty<double> BorderRadiusProperty =
        StyledProperty.Register<Control, double>("border-radius", 0, ValueParsers.Pixels);

    /// <summary>The text color. Inherited by descendants.</summary>
    public static readonly StyledProperty<Color> ColorProperty =
        StyledProperty.Register<Control, Color>("color", new Color(0, 0, 0), Color.TryParse, inherits: true);

    /// <summary>The font family. Inherited by descendants.</summary>
    public static readonly StyledProperty<string> FontFamilyProperty =
        StyledProperty.Register<Control, string>("font-family", "Segoe UI", ValueParsers.Text, inherits: true);

    /// <summary>The font size in pixels. Inherited by descendants.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        StyledProperty.Register<Control, double>("font-size", 14, ValueParsers.Pixels, inherits: true);

    /// <summary>Whether text is bold. Parsed from <c>bold</c>, <c>normal</c> or a numeric weight. Inherited by descendants.</summary>
    public static readonly StyledProperty<bool> BoldProperty =
        StyledProperty.Register<Control, bool>("font-weight", false, ValueParsers.Weight, inherits: true);


    /// <summary>The cached layout view of the style properties, or <see langword="null"/> after a property changed.</summary>
    LayoutStyle? layoutStyle;

    /// <summary>The text measurer set on this Control.</summary>
    ITextMeasurer? textMeasurer;

    /// <summary>The layer set on this Control, if any.</summary>
    OverlayLayer? overlays;


    /// <summary>
    /// Runs before the first Control is created, so that the Styled properties above are registered before any Stylesheet
    /// looks for them. See the remarks on <see cref="Element"/>.
    /// </summary>
    static Control()
    {
    }

    /// <summary>Gets the border box assigned by the last <see cref="Arrange"/>, relative to the parent's border box.</summary>
    public Rect Bounds { get; private set; }

    /// <summary>
    /// Gets or sets the object that measures text. When it is not set, the nearest ancestor's measurer is used, so it is
    /// normally set once on the root.
    /// </summary>
    public ITextMeasurer? TextMeasurer
    {
        get => textMeasurer ?? (Parent as Control)?.TextMeasurer;
        set => textMeasurer = value;
    }

    /// <summary>
    /// Gets or sets the layer that popups are shown in. When it is not set, the nearest ancestor's layer is used, so the
    /// platform window sets it once on the root.
    /// </summary>
    public OverlayLayer? Overlays
    {
        get => overlays ?? (Parent as Control)?.Overlays;
        set => overlays = value;
    }

    /// <summary>
    /// Gets or sets the menu that opens when the secondary pointer button is pressed on this Control or on one of its
    /// descendants that has none. The default is <see langword="null"/>.
    /// </summary>
    public ContextMenu? ContextMenu { get; set; }

    /// <summary>
    /// Gets or sets the text of the tooltip that appears when the pointer rests on this Control or on one of its descendants
    /// that has none. The default is <see langword="null"/>, which shows nothing.
    /// </summary>
    public string? Tooltip { get; set; }

    /// <inheritdoc/>
    LayoutStyle ILayoutNode.Style => layoutStyle ??= BuildLayoutStyle();

    /// <inheritdoc/>
    IReadOnlyList<ILayoutNode> ILayoutNode.Children => new LayoutChildren(Children);

    /// <summary>Sets or clears the <c>hover</c> Pseudo-state. The platform layer calls this as the pointer enters and leaves.</summary>
    /// <param name="value"><see langword="true"/> while the pointer is over the Control.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public bool SetHovered(bool value) => SetPseudoState("hover", value);

    /// <summary>Sets or clears the <c>active</c> Pseudo-state, which is set while the Control is pressed.</summary>
    /// <param name="value"><see langword="true"/> while the Control is pressed.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public bool SetPressed(bool value) => SetPseudoState("active", value);

    /// <summary>Gets whether the Control can have the keyboard focus. A click on it, or inside it, focuses it.</summary>
    public virtual bool IsFocusable => false;

    /// <summary>Sets or clears the <c>focus</c> Pseudo-state. The <see cref="FocusManager"/> calls this.</summary>
    /// <param name="value"><see langword="true"/> while the Control has the keyboard focus.</param>
    /// <returns><see langword="true"/> when the state changed, so styles need to be applied again.</returns>
    public bool SetFocused(bool value) => SetPseudoState("focus", value);

    /// <summary>Handles a key press while the Control has the keyboard focus.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the Control changed, so a new frame is needed. The default is <see langword="false"/>.</returns>
    public virtual bool KeyDown(Key key) => false;

    /// <summary>Handles typed text while the Control has the keyboard focus.</summary>
    /// <param name="text">The text, which can be several characters.</param>
    /// <returns><see langword="true"/> when the Control changed, so a new frame is needed. The default is <see langword="false"/>.</returns>
    public virtual bool TextInput(string text) => false;

    /// <summary>Lays out this Control and all of its descendants, and stores the result in <see cref="Bounds"/>.</summary>
    /// <param name="available">The area this Control lives in, such as the client area of a window. A Control without a width or height fills it.</param>
    public void Arrange(Size available) => FlexLayout.Arrange(this, available);

    /// <summary>Paints this Control and then its children into a drawing context.</summary>
    /// <param name="context">The context to draw on. Its origin is the parent's border box origin.</param>
    /// <remarks>Call <see cref="Arrange"/> first. Children paint in order, so later siblings are on top.</remarks>
    public void Render(IDrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Save();
        context.Translate(Bounds.X, Bounds.Y);

        OnRender(context, new Rect(0, 0, Bounds.Width, Bounds.Height));

        RenderChildren(context);

        context.Restore();
    }

    /// <summary>Converts a point from the root Control's coordinates to this Control's border box.</summary>
    /// <param name="x">The horizontal position, relative to the root Control.</param>
    /// <param name="y">The vertical position, relative to the root Control.</param>
    /// <returns>The position relative to this Control's border box, which may be outside it.</returns>
    /// <remarks>It undoes the positions and the scrolling of every ancestor, so call it after <see cref="Arrange"/>.</remarks>
    public (double X, double Y) ToLocal(double x, double y)
    {
        if (Parent is not Control parent)
            return (x - Bounds.X, y - Bounds.Y);

        var (parentX, parentY) = parent.ToLocal(x, y);
        var (offsetX, offsetY) = parent.ChildOffset;

        return (parentX - offsetX - Bounds.X, parentY - offsetY - Bounds.Y);
    }

    /// <summary>Finds the topmost Control at a point.</summary>
    /// <param name="x">The horizontal position, relative to this Control's border box.</param>
    /// <param name="y">The vertical position, relative to this Control's border box.</param>
    /// <returns>The deepest Control that contains the point, or <see langword="null"/> when the point is outside this Control.</returns>
    public Control? HitTest(double x, double y)
    {
        if (x < 0 || y < 0 || x >= Bounds.Width || y >= Bounds.Height)
            return null;

        if (ChildClip is { } clip && (x < clip.X || y < clip.Y || x >= clip.Right || y >= clip.Bottom))
            return this;

        var (offsetX, offsetY) = ChildOffset;

        for (var i = Children.Count - 1; i >= 0; i--)
        {
            var child = (Control)Children[i];

            var hit = child.HitTest(x - offsetX - child.Bounds.X, y - offsetY - child.Bounds.Y);

            if (hit is not null)
                return hit;
        }

        return this;
    }

    /// <summary>Paints the Control itself: its background and border. Override it to paint content, and call the base method first.</summary>
    /// <param name="context">The context to draw on.</param>
    /// <param name="bounds">The border box in the Control's own coordinates, so its origin is 0, 0.</param>
    protected virtual void OnRender(IDrawingContext context, Rect bounds)
    {
        var background = GetValue(BackgroundProperty);

        if (background.A > 0)
            context.FillRect(bounds, background, GetValue(BorderRadiusProperty));

        var border = BorderInsets(bounds.Width);
        var borderColor = GetValue(BorderColorProperty);

        if (borderColor.A == 0 || border.Top <= 0)
            return;

        var inset = border.Top / 2;
        var outline = new Rect(inset, inset, bounds.Width - border.Top, bounds.Height - border.Top);

        context.StrokeRect(outline, borderColor, border.Top, GetValue(BorderRadiusProperty));
    }

    /// <summary>Gets how far the children are moved from their laid-out position when they are painted and hit-tested.</summary>
    /// <remarks>A scrolling Control overrides it. The default is no offset.</remarks>
    protected virtual (double X, double Y) ChildOffset => default;

    /// <summary>Gets the area, in the Control's own coordinates, that the children are clipped to, or <see langword="null"/> for no clip.</summary>
    protected virtual Rect? ChildClip => null;

    /// <summary>Gets the main axis that the children are laid out on. A Control can override it to ignore <c>flex-direction</c>.</summary>
    protected virtual FlexDirection LayoutDirection => GetValue(FlexDirectionProperty);

    /// <summary>Builds the font description from the current property values.</summary>
    /// <returns>The text style used for both measuring and drawing.</returns>
    protected TextStyle CurrentTextStyle()
        => new(GetValue(FontFamilyProperty), GetValue(FontSizeProperty), GetValue(BoldProperty));

    /// <summary>Gets the text measurer, which must have been set on this Control or an ancestor.</summary>
    /// <returns>The measurer.</returns>
    /// <exception cref="InvalidOperationException">No measurer is set on this Control or any ancestor.</exception>
    protected ITextMeasurer Measurer() => TextMeasurer ?? throw new InvalidOperationException(
        $"{TypeName} needs a text measurer. Set Control.TextMeasurer on the root.");

    /// <summary>Measures the content of a Control that has no children, such as its text.</summary>
    /// <param name="availableWidth">The width the content may use, or <see langword="null"/> for its natural width.</param>
    /// <returns>The size of the content, without padding and border. The default is an empty size.</returns>
    protected virtual Size MeasureContent(double? availableWidth) => default;

    /// <summary>Gets the area inside the border and padding, in the Control's own coordinates.</summary>
    /// <param name="bounds">The border box.</param>
    /// <returns>The content box, never smaller than zero.</returns>
    protected Rect ContentBox(Rect bounds)
    {
        var border = BorderInsets(bounds.Width);
        var padding = Insets(GetValue(PaddingProperty), bounds.Width);

        var left = border.Left + padding.Left;
        var top = border.Top + padding.Top;

        var width = Math.Max(0, bounds.Width - left - border.Right - padding.Right);
        var height = Math.Max(0, bounds.Height - top - border.Bottom - padding.Bottom);

        return new Rect(left, top, width, height);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(StyledProperty property) => layoutStyle = null;

    /// <summary>Paints the children, moved by <see cref="ChildOffset"/> and clipped to <see cref="ChildClip"/>.</summary>
    /// <param name="context">The context to draw on, with its origin at this Control's border box.</param>
    void RenderChildren(IDrawingContext context)
    {
        if (Children.Count == 0)
            return;

        var (offsetX, offsetY) = ChildOffset;

        context.Save();

        if (ChildClip is { } clip)
            context.ClipRect(clip);

        context.Translate(offsetX, offsetY);

        foreach (var child in Children)
            ((Control)child).Render(context);

        context.Restore();
    }

    /// <inheritdoc/>
    Size ILayoutNode.Measure(double? availableWidth) => MeasureContent(availableWidth);

    /// <inheritdoc/>
    void ILayoutNode.SetLayout(Rect rect) => Bounds = rect;

    /// <summary>Builds the layout view of the current property values.</summary>
    /// <returns>A new style, which the caller caches.</returns>
    LayoutStyle BuildLayoutStyle() => new()
    {
        Direction = LayoutDirection,
        JustifyContent = GetValue(JustifyContentProperty),
        AlignItems = GetValue(AlignItemsProperty),
        AlignSelf = GetValue(AlignSelfProperty),
        Gap = GetValue(GapProperty),
        Grow = GetValue(FlexGrowProperty),
        Shrink = GetValue(FlexShrinkProperty),
        Basis = GetValue(FlexBasisProperty),
        Width = GetValue(WidthProperty),
        Height = GetValue(HeightProperty),
        MinWidth = GetValue(MinWidthProperty),
        MaxWidth = GetValue(MaxWidthProperty),
        MinHeight = GetValue(MinHeightProperty),
        MaxHeight = GetValue(MaxHeightProperty),
        Padding = GetValue(PaddingProperty),
        Border = GetValue(BorderWidthProperty),
        Margin = GetValue(MarginProperty),
    };

    /// <summary>Resolves the border widths against the Control's width.</summary>
    /// <param name="width">The width that percentages resolve against.</param>
    /// <returns>The widths in pixels.</returns>
    (double Top, double Right, double Bottom, double Left) BorderInsets(double width)
        => Insets(GetValue(BorderWidthProperty), width);

    /// <summary>Resolves a thickness to pixels, treating auto and negative values as zero.</summary>
    /// <param name="thickness">The thickness.</param>
    /// <param name="width">The width that percentages resolve against.</param>
    /// <returns>The four sides in pixels.</returns>
    static (double Top, double Right, double Bottom, double Left) Insets(Thickness thickness, double width) =>
    (
        Math.Max(0, Lengths.Resolve(thickness.Top, width) ?? 0),
        Math.Max(0, Lengths.Resolve(thickness.Right, width) ?? 0),
        Math.Max(0, Lengths.Resolve(thickness.Bottom, width) ?? 0),
        Math.Max(0, Lengths.Resolve(thickness.Left, width) ?? 0)
    );

    /// <summary>A read-only list view over the children that exposes each one as a layout node.</summary>
    /// <param name="children">The Element children to wrap.</param>
    sealed class LayoutChildren(ElementCollection children) : IReadOnlyList<ILayoutNode>
    {
        /// <inheritdoc/>
        public int Count => children.Count;

        /// <inheritdoc/>
        public ILayoutNode this[int index] => children[index] as ILayoutNode ?? throw NotAControl(children[index]);

        /// <summary>Creates the exception for a child that is not a Control.</summary>
        /// <param name="child">The child.</param>
        /// <returns>The exception to throw.</returns>
        static InvalidOperationException NotAControl(Element child) =>
            new($"{child.TypeName} is not a Control. Only Controls can be children of a Control.");

        /// <inheritdoc/>
        public IEnumerator<ILayoutNode> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
                yield return this[i];
        }

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

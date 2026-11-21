using Silo.Layout;
using Silo.Styling.Values;

public class TestNode(
    LayoutStyle? style = null,
    Func<double?, Size>? measure = null,
    params TestNode[] children) : ILayoutNode
{
    public LayoutStyle Style { get; } = style ?? new LayoutStyle();
    public IReadOnlyList<ILayoutNode> Children { get; } = children;
    public Rect Rect { get; private set; }

    public Size Measure(double? availableWidth) => measure?.Invoke(availableWidth) ?? default;
    public void SetLayout(Rect rect) => Rect = rect;

    public static Length Px(double value) => new(value, LengthUnit.Px);
    public static Thickness All(double value) => new(Px(value), Px(value), Px(value), Px(value));
}

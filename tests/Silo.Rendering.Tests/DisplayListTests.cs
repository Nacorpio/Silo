using Silo.Layout;
using Silo.Rendering;
using Silo.Styling.Values;
using Xunit;

public class DisplayListTests
{
    /// <summary>Records every call as text.</summary>
    class Log : IDrawingContext
    {
        public List<string> Calls { get; } = [];

        public void Save() => Calls.Add("save");
        public void Restore() => Calls.Add("restore");

        public void Translate(double dx, double dy) => Calls.Add($"translate {dx} {dy}");
        public void ClipRect(Rect rect) => Calls.Add($"clip {rect.X} {rect.Y} {rect.Width} {rect.Height}");
        public void FillRect(Rect rect, Color color, double cornerRadius = 0)
            => Calls.Add($"fill {rect.Width}x{rect.Height} {color.R} r{cornerRadius}");
        public void ClipRoundRect(Rect rect, double cornerRadius) => Calls.Add($"roundclip {rect.Width} {cornerRadius}");
        public void StrokePolyline(IReadOnlyList<(double X, double Y)> points, Color color, double width, bool closed = false)
            => Calls.Add($"polyline {points.Count} {color.R} {width} {closed}");
        public void FillGradient(Rect rect, Color from, Color to, bool vertical = false)
            => Calls.Add($"gradient {rect.Width} {from.R} {to.R} {vertical}");
        public void StrokeRect(Rect rect, Color color, double width, double cornerRadius = 0)
            => Calls.Add($"stroke {width}");
        public void DrawText(string text, double x, double y, TextStyle style, Color color)
            => Calls.Add($"text {text} {x} {y} {style.Size}");
        public void DrawImage(IImage image, Rect destination)
            => Calls.Add($"image {image.Width}x{image.Height} {destination.X} {destination.Y} {destination.Width}");
        public void DrawMask(IImage image, Rect destination, Color color)
            => Calls.Add($"mask {image.Width}x{image.Height} {destination.X} {color.R}");
    }

    sealed record Picture(int Width, int Height) : IImage;

    [Fact]
    public void Replay_calls_the_context_in_recorded_order()
    {
        var builder = new DisplayListBuilder();

        builder.Save();
        builder.Translate(5, 6);
        builder.ClipRect(new Rect(0, 0, 10, 10));
        builder.FillRect(new Rect(0, 0, 10, 4), new Color(9, 0, 0), 2);
        builder.FillGradient(new Rect(0, 0, 10, 4), new Color(1, 0, 0), new Color(2, 0, 0), vertical: true);
        builder.ClipRoundRect(new Rect(0, 0, 10, 4), 2);
        builder.StrokePolyline([(0, 0), (5, 5), (10, 0)], new Color(3, 0, 0), 2, closed: true);
        builder.StrokeRect(new Rect(0, 0, 10, 4), new Color(0, 0, 0), 1);
        builder.DrawText("Hi", 1, 2, new TextStyle("Inter", 14), new Color(0, 0, 0));
        builder.DrawImage(new Picture(8, 6), new Rect(3, 4, 16, 12));
        builder.DrawMask(new Picture(8, 6), new Rect(1, 2, 16, 12), new Color(7, 0, 0));
        builder.Restore();

        var log = new Log();

        builder.Build().Replay(log);

        string[] expected =
        [
            "save", "translate 5 6", "clip 0 0 10 10", "fill 10x4 9 r2", "gradient 10 1 2 True", "roundclip 10 2", "polyline 3 3 2 True", "stroke 1", "text Hi 1 2 14", "image 8x6 3 4 16", "mask 8x6 1 7", "restore",
        ];

        Assert.Equal(expected, log.Calls);
    }

    [Fact]
    public void A_built_list_is_not_changed_by_later_calls_on_the_builder()
    {
        var builder = new DisplayListBuilder();

        builder.FillRect(new Rect(0, 0, 1, 1), new Color(0, 0, 0));

        var list = builder.Build();

        builder.FillRect(new Rect(0, 0, 2, 2), new Color(0, 0, 0));

        Assert.Single(list.Commands);
        Assert.Equal(2, builder.Count);
    }

    [Fact]
    public void Unbalanced_save_and_restore_are_rejected()
    {
        var unmatchedRestore = new DisplayListBuilder();
        var unmatchedSave = new DisplayListBuilder();

        unmatchedSave.Save();

        Assert.Throws<InvalidOperationException>(unmatchedRestore.Restore);
        Assert.Throws<InvalidOperationException>(unmatchedSave.Build);
    }

    [Fact]
    public void Commands_are_value_equal()
    {
        Assert.Equal(new TranslateCommand(1, 2), new TranslateCommand(1, 2));
        Assert.NotEqual(new TranslateCommand(1, 2), new TranslateCommand(1, 3));
    }
}

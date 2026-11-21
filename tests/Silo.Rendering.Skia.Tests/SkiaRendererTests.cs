using Silo.Layout;
using Silo.Rendering;
using Silo.Rendering.Skia;
using Silo.Styling.Values;
using SkiaSharp;
using Xunit;

public class SkiaRendererTests
{
    static readonly Color White = new(255, 255, 255);
    static readonly Color Blue = new(0, 0, 255);

    static SKColor PixelAt(SKBitmap bitmap, int x, int y) => bitmap.GetPixel(x, y);

    [Fact]
    public void Fill_rect_paints_inside_and_leaves_the_outside_alone()
    {
        var builder = new DisplayListBuilder();

        builder.FillRect(new Rect(10, 10, 20, 20), Blue);

        using var bitmap = SkiaRenderer.Render(builder.Build(), 50, 50, White);

        Assert.Equal(new SKColor(0, 0, 255), PixelAt(bitmap, 20, 20));
        Assert.Equal(new SKColor(255, 255, 255), PixelAt(bitmap, 5, 5));
        Assert.Equal(new SKColor(255, 255, 255), PixelAt(bitmap, 35, 35));
    }

    [Fact]
    public void Translate_moves_later_drawing_and_restore_undoes_it()
    {
        var builder = new DisplayListBuilder();

        builder.Save();
        builder.Translate(20, 20);
        builder.FillRect(new Rect(0, 0, 10, 10), Blue);
        builder.Restore();
        builder.FillRect(new Rect(0, 0, 5, 5), new Color(255, 0, 0));

        using var bitmap = SkiaRenderer.Render(builder.Build(), 50, 50, White);

        Assert.Equal(new SKColor(0, 0, 255), PixelAt(bitmap, 25, 25));
        Assert.Equal(new SKColor(255, 0, 0), PixelAt(bitmap, 2, 2));
        Assert.Equal(new SKColor(255, 255, 255), PixelAt(bitmap, 5, 5));
    }

    [Fact]
    public void Clip_rect_limits_drawing()
    {
        var builder = new DisplayListBuilder();

        builder.Save();
        builder.ClipRect(new Rect(0, 0, 10, 10));
        builder.FillRect(new Rect(0, 0, 40, 40), Blue);
        builder.Restore();

        using var bitmap = SkiaRenderer.Render(builder.Build(), 50, 50, White);

        Assert.Equal(new SKColor(0, 0, 255), PixelAt(bitmap, 5, 5));
        Assert.Equal(new SKColor(255, 255, 255), PixelAt(bitmap, 20, 20));
    }

    [Fact]
    public void Rounded_corners_leave_the_corner_pixel_unpainted()
    {
        var builder = new DisplayListBuilder();

        builder.FillRect(new Rect(0, 0, 40, 40), Blue, 12);

        using var bitmap = SkiaRenderer.Render(builder.Build(), 40, 40, White);

        Assert.Equal(new SKColor(255, 255, 255), PixelAt(bitmap, 0, 0));
        Assert.Equal(new SKColor(0, 0, 255), PixelAt(bitmap, 20, 20));
    }

    [Fact]
    public void Stroke_rect_paints_the_edge_and_not_the_middle()
    {
        var builder = new DisplayListBuilder();

        builder.StrokeRect(new Rect(5, 5, 30, 30), Blue, 2);

        using var bitmap = SkiaRenderer.Render(builder.Build(), 40, 40, White);

        Assert.Equal(new SKColor(0, 0, 255), PixelAt(bitmap, 20, 5));
        Assert.Equal(new SKColor(255, 255, 255), PixelAt(bitmap, 20, 20));
    }

    [Fact]
    public void Restore_without_save_is_rejected()
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);

        var context = new SkiaDrawingContext(canvas);

        Assert.Throws<InvalidOperationException>(context.Restore);
    }

    [Fact]
    public void Text_measures_wider_with_more_characters_and_a_bigger_size()
    {
        var measurer = new SkiaTextMeasurer();
        var style = new TextStyle("sans-serif", 14);

        var short_ = measurer.Measure("Hi", style);
        var long_ = measurer.Measure("Hello world", style);
        var big = measurer.Measure("Hi", style with { Size = 28 });

        Assert.True(long_.Width > short_.Width);
        Assert.True(big.Width > short_.Width);
        Assert.True(big.Height > short_.Height);
    }

    [Fact]
    public void Drawn_text_paints_pixels_inside_its_measured_box()
    {
        var style = new TextStyle("sans-serif", 20);
        var box = new SkiaTextMeasurer().Measure("Hello", style);

        var builder = new DisplayListBuilder();

        builder.DrawText("Hello", 4, 4, style, new Color(0, 0, 0));

        using var bitmap = SkiaRenderer.Render(builder.Build(), 120, 60, White);

        var painted = 0;

        for (var y = 4; y < 4 + (int)box.Height; y++)
            for (var x = 4; x < 4 + (int)box.Width; x++)
                if (PixelAt(bitmap, x, y) != new SKColor(255, 255, 255))
                    painted++;

        Assert.True(painted > 20, $"only {painted} pixels were painted");
    }

    [Fact]
    public void Encode_produces_a_png()
    {
        var builder = new DisplayListBuilder();

        using var bitmap = SkiaRenderer.Render(builder.Build(), 4, 4, White);

        var bytes = SkiaRenderer.Encode(bitmap);

        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);
    }
}

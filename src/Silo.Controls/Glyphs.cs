namespace Silo;

/// <summary>
/// The built-in vector glyphs that an <see cref="Icon"/> can show by name.
/// </summary>
/// <remarks>
/// <para>
/// Every glyph is a few lines on a 24 by 24 grid, drawn with a round pen, so they scale to any size and take the color of the
/// text. The names are lower case: <c>check</c>, <c>close</c>, <c>plus</c>, <c>minus</c>, <c>chevron-left</c>,
/// <c>chevron-right</c>, <c>chevron-up</c>, <c>chevron-down</c>, <c>arrow-left</c>, <c>arrow-right</c>, <c>arrow-up</c>,
/// <c>arrow-down</c>, <c>search</c>, <c>menu</c>, <c>star</c>, <c>info</c>, <c>warning</c>, <c>error</c>, <c>user</c>,
/// <c>calendar</c> and <c>home</c>.
/// </para>
/// </remarks>
public static class Glyphs
{
    /// <summary>The size of the grid that the glyphs are drawn on.</summary>
    public const double Grid = 24;


    /// <summary>One line of a glyph: its points, and whether the last joins the first.</summary>
    /// <param name="Points">The points on the grid.</param>
    /// <param name="Closed">Whether the line is a closed shape.</param>
    public sealed record Line(IReadOnlyList<(double X, double Y)> Points, bool Closed = false);


    /// <summary>The glyphs by name.</summary>
    static readonly Dictionary<string, IReadOnlyList<Line>> Set = new()
    {
        ["check"] = [Open((4, 12), (10, 18), (20, 6))],
        ["close"] = [Open((5, 5), (19, 19)), Open((19, 5), (5, 19))],
        ["plus"] = [Open((12, 4), (12, 20)), Open((4, 12), (20, 12))],
        ["minus"] = [Open((4, 12), (20, 12))],
        ["chevron-left"] = [Open((15, 5), (8, 12), (15, 19))],
        ["chevron-right"] = [Open((9, 5), (16, 12), (9, 19))],
        ["chevron-up"] = [Open((5, 15), (12, 8), (19, 15))],
        ["chevron-down"] = [Open((5, 9), (12, 16), (19, 9))],
        ["arrow-left"] = [Open((20, 12), (4, 12)), Open((10, 6), (4, 12), (10, 18))],
        ["arrow-right"] = [Open((4, 12), (20, 12)), Open((14, 6), (20, 12), (14, 18))],
        ["arrow-up"] = [Open((12, 20), (12, 4)), Open((6, 10), (12, 4), (18, 10))],
        ["arrow-down"] = [Open((12, 4), (12, 20)), Open((6, 14), (12, 20), (18, 14))],
        ["search"] = [new Line(Circle(10.5, 10.5, 6.5), Closed: true), Open((15.5, 15.5), (20, 20))],
        ["menu"] = [Open((4, 6), (20, 6)), Open((4, 12), (20, 12)), Open((4, 18), (20, 18))],
        ["star"] = [new Line(Star(12, 12.5, 9, 3.8), Closed: true)],
        ["info"] = [new Line(Circle(12, 12, 9), Closed: true), Open((12, 11), (12, 16.5)), Open((12, 7.5), (12, 7.6))],
        ["warning"] = [new Line([(12, 3.5), (21.5, 20), (2.5, 20)], Closed: true), Open((12, 10), (12, 14.5)), Open((12, 17), (12, 17.1))],
        ["error"] = [new Line(Circle(12, 12, 9), Closed: true), Open((12, 7), (12, 13)), Open((12, 16.4), (12, 16.5))],
        ["user"] = [new Line(Circle(12, 8, 4), Closed: true), Open((4, 21), (5, 17), (8.5, 14.5), (12, 14), (15.5, 14.5), (19, 17), (20, 21))],
        ["calendar"] = [new Line([(4, 6), (20, 6), (20, 20), (4, 20)], Closed: true), Open((4, 11), (20, 11)), Open((8, 3), (8, 8)), Open((16, 3), (16, 8))],
        ["activity"] = [Open((2, 12), (7, 12), (10, 4), (14, 20), (17, 12), (22, 12))],
        ["eye"] = [new Line([(2, 12), (7, 7), (12, 5.5), (17, 7), (22, 12), (17, 17), (12, 18.5), (7, 17)], Closed: true), new Line(Circle(12, 12, 3), Closed: true)],
        ["fork"] = [new Line(Circle(6, 5, 2.5), Closed: true), new Line(Circle(18, 5, 2.5), Closed: true), new Line(Circle(12, 19, 2.5), Closed: true), Open((6, 7.5), (6, 10), (18, 10), (18, 7.5)), Open((12, 10), (12, 16.5))],
        ["list"] = [Open((9, 6), (20, 6)), Open((9, 12), (20, 12)), Open((9, 18), (20, 18)), Open((4, 6), (4.1, 6)), Open((4, 12), (4.1, 12)), Open((4, 18), (4.1, 18))],
        ["grid"] = [new Line([(4, 4), (10.5, 4), (10.5, 10.5), (4, 10.5)], Closed: true), new Line([(13.5, 4), (20, 4), (20, 10.5), (13.5, 10.5)], Closed: true), new Line([(4, 13.5), (10.5, 13.5), (10.5, 20), (4, 20)], Closed: true), new Line([(13.5, 13.5), (20, 13.5), (20, 20), (13.5, 20)], Closed: true)],
        ["image"] = [new Line([(3, 5), (21, 5), (21, 19), (3, 19)], Closed: true), Open((3, 16), (9, 10), (14, 15), (17, 12), (21, 16)), new Line(Circle(8, 9, 1.5), Closed: true)],
        ["sliders"] = [Open((4, 8), (20, 8)), Open((4, 16), (20, 16)), new Line(Circle(9, 8, 2), Closed: true), new Line(Circle(15, 16, 2), Closed: true)],
        ["layout"] = [new Line([(3, 4), (21, 4), (21, 20), (3, 20)], Closed: true), Open((3, 9), (21, 9)), Open((9, 9), (9, 20))],
        ["pencil"] = [new Line([(4, 20), (5, 15), (16, 4), (20, 8), (9, 19)], Closed: true), Open((14, 6), (18, 10))],
        ["maximize"] = [new Line([(5, 5), (19, 5), (19, 19), (5, 19)], Closed: true)],
        ["restore"] = [new Line([(5, 8), (16, 8), (16, 19), (5, 19)], Closed: true), Open((8, 8), (8, 5), (19, 5), (19, 16), (16, 16))],
        ["home"] = [Open((3, 11), (12, 3), (21, 11)), Open((5, 9.5), (5, 21), (19, 21), (19, 9.5))],
    };


    /// <summary>Gets the names of the glyphs, in alphabetical order.</summary>
    public static IEnumerable<string> Names => Set.Keys.Order();

    /// <summary>Looks up a glyph.</summary>
    /// <param name="name">The name, in lower case.</param>
    /// <param name="lines">The lines of the glyph on a <see cref="Grid"/> by <see cref="Grid"/> grid.</param>
    /// <returns><see langword="true"/> when there is a glyph of that name.</returns>
    public static bool TryGet(string name, out IReadOnlyList<Line> lines)
    {
        if (Set.TryGetValue(name, out var found))
        {
            lines = found;

            return true;
        }

        lines = [];

        return false;
    }

    /// <summary>Makes a line that is not closed.</summary>
    /// <param name="points">The points.</param>
    /// <returns>The line.</returns>
    static Line Open(params (double X, double Y)[] points) => new(points);

    /// <summary>Makes the points of a circle.</summary>
    /// <param name="x">The center.</param>
    /// <param name="y">The center.</param>
    /// <param name="radius">The radius.</param>
    /// <returns>Twenty-four points around it.</returns>
    static (double X, double Y)[] Circle(double x, double y, double radius)
        => [.. Enumerable.Range(0, 24).Select(i => (x + (radius * Math.Cos(i * Math.PI / 12)), y + (radius * Math.Sin(i * Math.PI / 12))))];

    /// <summary>Makes the points of a five pointed star.</summary>
    /// <param name="x">The center.</param>
    /// <param name="y">The center.</param>
    /// <param name="outer">The radius of the tips.</param>
    /// <param name="inner">The radius of the notches.</param>
    /// <returns>Ten points, starting at the top tip.</returns>
    static (double X, double Y)[] Star(double x, double y, double outer, double inner)
        => [.. Enumerable.Range(0, 10).Select(i =>
        {
            var radius = i % 2 == 0 ? outer : inner;
            var angle = (i * Math.PI / 5) - (Math.PI / 2);

            return (x + (radius * Math.Cos(angle)), y + (radius * Math.Sin(angle)));
        })];
}

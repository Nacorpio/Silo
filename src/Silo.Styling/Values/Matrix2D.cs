namespace Silo.Styling.Values;

/// <summary>
/// A 2D affine transform, the matrix with columns (A, B), (C, D) and the translation (E, F). A point <c>(x, y)</c> maps to
/// <c>(A·x + C·y + E, B·x + D·y + F)</c>.
/// </summary>
/// <param name="A">The horizontal scale and cosine part.</param>
/// <param name="B">The vertical shear and sine part.</param>
/// <param name="C">The horizontal shear and negative sine part.</param>
/// <param name="D">The vertical scale and cosine part.</param>
/// <param name="E">The horizontal translation.</param>
/// <param name="F">The vertical translation.</param>
public readonly record struct Matrix2D(double A, double B, double C, double D, double E, double F)
{
    /// <summary>Gets the matrix that leaves every point where it is.</summary>
    public static Matrix2D Identity => new(1, 0, 0, 1, 0, 0);

    /// <summary>Gets whether this is the identity matrix.</summary>
    public bool IsIdentity => this == Identity;

    /// <summary>Gets the determinant of the linear part, which is 0 when the matrix cannot be undone.</summary>
    public double Determinant => (A * D) - (B * C);

    /// <summary>Creates a matrix that moves points.</summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <returns>The matrix.</returns>
    public static Matrix2D Translation(double dx, double dy) => new(1, 0, 0, 1, dx, dy);

    /// <summary>Creates a matrix that scales around the origin.</summary>
    /// <param name="sx">The horizontal factor.</param>
    /// <param name="sy">The vertical factor.</param>
    /// <returns>The matrix.</returns>
    public static Matrix2D Scaling(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);

    /// <summary>Creates a matrix that rotates clockwise around the origin, in a space where y grows downwards.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The matrix.</returns>
    public static Matrix2D Rotation(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var (sin, cos) = Math.SinCos(radians);

        return new Matrix2D(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>Combines two matrices, so that the result applies <paramref name="next"/> first and then this matrix.</summary>
    /// <param name="next">The matrix that is applied to the points before this one.</param>
    /// <returns>The product <c>this · next</c>.</returns>
    public Matrix2D Multiply(Matrix2D next) => new(
        (A * next.A) + (C * next.B),
        (B * next.A) + (D * next.B),
        (A * next.C) + (C * next.D),
        (B * next.C) + (D * next.D),
        (A * next.E) + (C * next.F) + E,
        (B * next.E) + (D * next.F) + F);

    /// <summary>Maps a point.</summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <returns>The mapped position.</returns>
    public (double X, double Y) Apply(double x, double y) => ((A * x) + (C * y) + E, (B * x) + (D * y) + F);

    /// <summary>Tries to find the matrix that undoes this one.</summary>
    /// <param name="inverse">The inverse, or the identity when there is none.</param>
    /// <returns><see langword="false"/> when the determinant is 0, for example after scaling to nothing.</returns>
    public bool TryInvert(out Matrix2D inverse)
    {
        var determinant = Determinant;

        if (Math.Abs(determinant) < 1e-12 || double.IsNaN(determinant))
        {
            inverse = Identity;

            return false;
        }

        inverse = new Matrix2D(
            D / determinant,
            -B / determinant,
            -C / determinant,
            A / determinant,
            ((C * F) - (D * E)) / determinant,
            ((B * E) - (A * F)) / determinant);

        return true;
    }
}

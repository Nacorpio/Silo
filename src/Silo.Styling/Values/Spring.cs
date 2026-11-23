namespace Silo.Styling.Values;

/// <summary>
/// A damped spring that pulls a value to its target. It does not have a duration: it runs until it has settled, and a spring
/// that is interrupted keeps its speed, which is what makes it feel natural.
/// </summary>
/// <param name="Stiffness">How hard the spring pulls. Higher values are faster. A typical value is 170.</param>
/// <param name="Damping">How much it holds back. Higher values overshoot less. A typical value is 18.</param>
/// <param name="Mass">How heavy the moving value is. Higher values are slower to start and stop. The default is 1.</param>
public readonly record struct Spring(double Stiffness, double Damping, double Mass = 1)
{
    /// <summary>The longest that a spring is simulated, in seconds, however it is set up.</summary>
    const double MaxSeconds = 10;

    /// <summary>The distance to the target, as a fraction of the change, below which a spring counts as settled.</summary>
    const double PositionTolerance = 0.0005;

    /// <summary>The speed, in changes per second, below which a spring counts as settled.</summary>
    const double VelocityTolerance = 0.005;

    /// <summary>Gets whether the numbers describe a spring that can be simulated.</summary>
    public bool IsValid => double.IsFinite(Stiffness) && double.IsFinite(Damping) && double.IsFinite(Mass) && Stiffness > 0 && Damping >= 0 && Mass > 0;

    /// <summary>Works out how far a value that starts at rest has come.</summary>
    /// <param name="seconds">The time since the start.</param>
    /// <param name="velocity">
    /// The speed at the start, in changes per second: 1 means the whole change per second towards the target. The default is 0, which
    /// starts from rest.
    /// </param>
    /// <returns>The fraction of the change, 0 at the start and close to 1 at the end. It goes above 1 when the spring overshoots.</returns>
    public double Progress(double seconds, double velocity = 0) => 1 + Displacement(seconds, velocity);

    /// <summary>Works out how fast the value moves.</summary>
    /// <param name="seconds">The time since the start.</param>
    /// <param name="velocity">The speed at the start, in changes per second.</param>
    /// <returns>The speed in changes per second.</returns>
    public double SpeedAt(double seconds, double velocity = 0)
    {
        if (seconds <= 0)
            return velocity;

        const double step = 1.0 / 2000;

        var (before, after) = (Math.Max(0, seconds - step), seconds + step);

        return (Displacement(after, velocity) - Displacement(before, velocity)) / (after - before);
    }

    /// <summary>Works out how long the spring takes to settle.</summary>
    /// <param name="velocity">The speed at the start, in changes per second.</param>
    /// <returns>The time until the value is close enough to the target and nearly still, never more than 10 seconds.</returns>
    public TimeSpan SettleTime(double velocity = 0)
    {
        if (!IsValid)
            return TimeSpan.Zero;

        const double step = 1.0 / 120;

        var last = 0.0;

        for (var t = 0.0; t < MaxSeconds; t += step)
        {
            if (Math.Abs(Displacement(t, velocity)) > PositionTolerance || Math.Abs(SpeedAt(t, velocity)) > VelocityTolerance)
                last = t + step;
        }

        return TimeSpan.FromSeconds(Math.Min(last, MaxSeconds));
    }

    /// <summary>Tries to read <c>spring(stiffness damping)</c> or <c>spring(stiffness damping mass)</c>.</summary>
    /// <param name="text">The text. The numbers are separated by white space or commas.</param>
    /// <param name="spring">The spring.</param>
    /// <returns><see langword="false"/> when the text is not a spring or the numbers do not describe one.</returns>
    public static bool TryParse(string text, out Spring spring)
    {
        spring = default;

        if (!Function.TryRead(text.Trim(), out var name, out var arguments) || name != "spring" || arguments.Length is < 2 or > 3)
            return false;

        var numbers = new double[arguments.Length];

        for (var i = 0; i < arguments.Length; i++)
            if (!double.TryParse(arguments[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out numbers[i]))
                return false;

        spring = new Spring(numbers[0], numbers[1], numbers.Length == 3 ? numbers[2] : 1);

        return spring.IsValid;
    }

    /// <summary>Writes the spring the way a Stylesheet would.</summary>
    /// <returns>The function with all three numbers.</returns>
    public override string ToString() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"spring({Stiffness} {Damping} {Mass})");

    /// <summary>Works out how far the value is from the target, as a fraction of the change; it starts at -1.</summary>
    double Displacement(double seconds, double velocity)
    {
        if (seconds <= 0)
            return -1;

        var omega = Math.Sqrt(Stiffness / Mass);
        var zeta = Damping / (2 * Math.Sqrt(Stiffness * Mass));
        var (x0, v0) = (-1.0, velocity);

        if (zeta < 1 - 1e-9)
        {
            var damped = omega * Math.Sqrt(1 - (zeta * zeta));
            var b = (v0 + (zeta * omega * x0)) / damped;

            return Math.Exp(-zeta * omega * seconds) * ((x0 * Math.Cos(damped * seconds)) + (b * Math.Sin(damped * seconds)));
        }

        if (zeta <= 1 + 1e-9)
            return Math.Exp(-omega * seconds) * (x0 + ((v0 + (omega * x0)) * seconds));

        var root = Math.Sqrt((zeta * zeta) - 1);
        var (r1, r2) = (-omega * (zeta - root), -omega * (zeta + root));
        var c2 = (v0 - (r1 * x0)) / (r2 - r1);
        var c1 = x0 - c2;

        return (c1 * Math.Exp(r1 * seconds)) + (c2 * Math.Exp(r2 * seconds));
    }
}

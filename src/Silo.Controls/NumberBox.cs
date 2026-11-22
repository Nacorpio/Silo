using System.Globalization;

namespace Silo;

/// <summary>
/// A single line of text that only accepts a number, with a minimum, a maximum and a step.
/// </summary>
/// <remarks>
/// <para>
/// The NumberBox is a <see cref="TextBox"/> that refuses every edit that could not become a number: digits, one decimal
/// point and, when <see cref="Minimum"/> is below zero, a leading minus sign. <see cref="Value"/> follows the text while the
/// user types, limited to <see cref="Minimum"/> and <see cref="Maximum"/>, and raises <see cref="ValueChanged"/>. While the
/// text is not a number yet, such as <c>-</c> or empty, <see cref="Value"/> keeps its last number.
/// </para>
/// <para>
/// The Up and Down arrows add and subtract <see cref="Step"/>, and Enter rewrites the text from <see cref="Value"/>, so that
/// <c>007</c> becomes <c>7</c> and a number above <see cref="Maximum"/> shows the maximum. Numbers are written with the
/// invariant culture.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var quantity = new NumberBox { Minimum = 1, Maximum = 99, Value = 1 };
///
/// quantity.ValueChanged += (_, _) => cart.SetQuantity((int)quantity.Value);
/// </code>
/// </example>
public class NumberBox : TextBox
{
    /// <summary>The number.</summary>
    double value;

    /// <summary>The smallest allowed number.</summary>
    double minimum = double.MinValue;

    /// <summary>The largest allowed number.</summary>
    double maximum = double.MaxValue;

    /// <summary>Whether the text is being written from <see cref="Value"/>, so the change must not be read back.</summary>
    bool writing;


    /// <summary>Creates a NumberBox that shows 0.</summary>
    public NumberBox()
    {
        Write();

        TextChanged += (_, _) => Read();
    }

    /// <summary>Raised after <see cref="Value"/> changed, whether the user typed it or code set it.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Gets or sets the number. Setting it limits it to <see cref="Minimum"/> and <see cref="Maximum"/> and rewrites the text.</summary>
    public double Value
    {
        get => value;
        set
        {
            var next = Math.Clamp(value, minimum, maximum);

            if (next == this.value)
            {
                Write();

                return;
            }

            this.value = next;

            Write();

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the smallest allowed number. The default is <see cref="double.MinValue"/>.</summary>
    public double Minimum
    {
        get => minimum;
        set
        {
            minimum = value;
            maximum = Math.Max(maximum, value);
            Value = this.value;
        }
    }

    /// <summary>Gets or sets the largest allowed number. The default is <see cref="double.MaxValue"/>.</summary>
    public double Maximum
    {
        get => maximum;
        set
        {
            maximum = value;
            minimum = Math.Min(minimum, value);
            Value = this.value;
        }
    }

    /// <summary>Gets or sets how much the Up and Down arrows change <see cref="Value"/>. The default is 1.</summary>
    public double Step { get; set; } = 1;

    /// <inheritdoc/>
    public override bool KeyDown(Key key)
    {
        switch (key)
        {
            case Key.Up:
                Value += Step;
                return true;

            case Key.Down:
                Value -= Step;
                return true;

            case Key.Enter:
                Value = value;
                break;
        }

        return base.KeyDown(key);
    }

    /// <inheritdoc/>
    protected override bool Accepts(string candidate)
    {
        var digits = candidate.StartsWith('-') && minimum < 0 ? candidate[1..] : candidate;

        return digits.Count(c => c == '.') <= 1 && digits.All(c => char.IsAsciiDigit(c) || c == '.');
    }

    /// <summary>Takes the number from the text, when the text is one.</summary>
    void Read()
    {
        if (writing)
            return;

        if (!double.TryParse(Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsed))
            return;

        var next = Math.Clamp(parsed, minimum, maximum);

        if (next == value)
            return;

        value = next;

        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes <see cref="Value"/> as the text.</summary>
    void Write()
    {
        writing = true;
        Text = value.ToString("0.#############", CultureInfo.InvariantCulture);
        writing = false;
    }
}

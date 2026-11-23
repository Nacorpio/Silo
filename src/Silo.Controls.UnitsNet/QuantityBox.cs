using System.Globalization;
using UnitsNet;

namespace Silo;

/// <summary>
/// A field for a physical quantity: a number and a dropdown with its unit, backed by UnitsNet.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="QuantityType"/> to the kind of quantity, such as <c>"Length"</c>, and the dropdown offers the units of that
/// kind, labelled with UnitsNet's abbreviations for <see cref="Culture"/>. <see cref="Value"/> holds the quantity that the
/// number and the unit make together. The box is not generic, so a Stylesheet can name it and a View can declare it.
/// </para>
/// <para>
/// Typing in the number, stepping it with the Up and Down arrows, or picking another unit changes <see cref="Value"/> and
/// raises <see cref="ValueChanged"/>. When another unit is picked, the number is converted (1 m becomes 100 cm) unless
/// <see cref="ConvertOnUnitChange"/> is <see langword="false"/>, which keeps the digits and changes what they mean. The number
/// is shown with <see cref="SignificantDigits"/> digits; <see cref="Value"/> keeps its full precision.
/// </para>
/// <para>
/// <see cref="Minimum"/> and <see cref="Maximum"/> limit the quantity. They are quantities themselves, so they hold in
/// every unit. The look comes from <see cref="QuantityTheme"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var length = new QuantityBox { QuantityType = "Length", Maximum = Length.FromMeters(100) };
///
/// length.ValueChanged += (_, _) => model.Width = (Length)length.Value!;
/// </code>
/// </example>
public class QuantityBox : Control
{
    /// <summary>The most significant digits that a double keeps.</summary>
    const int MaximumDigits = 15;

    /// <summary>The number part.</summary>
    readonly NumberBox number = new();

    /// <summary>The line between the number and the unit.</summary>
    readonly Separator divider = new();

    /// <summary>The unit dropdown.</summary>
    readonly ComboBox unitBox = new();

    /// <summary>The kind of quantity, or <see langword="null"/> while none is set.</summary>
    QuantityInfo? info;

    /// <summary>The units that the dropdown offers.</summary>
    IReadOnlyList<Enum> units = [];

    /// <summary>The selected unit.</summary>
    Enum? unit;

    /// <summary>The quantity.</summary>
    IQuantity? value;

    /// <summary>The smallest allowed quantity.</summary>
    IQuantity? minimum;

    /// <summary>The largest allowed quantity.</summary>
    IQuantity? maximum;

    /// <summary>The culture of the unit labels.</summary>
    CultureInfo culture = CultureInfo.CurrentCulture;

    /// <summary>How many digits the number shows.</summary>
    int significantDigits = 6;

    /// <summary>Whether the number is converted when another unit is picked.</summary>
    bool convertOnUnitChange = true;

    /// <summary>Whether the parts are being written from <see cref="Value"/>, so that their events must not be read back.</summary>
    bool syncing;


    /// <summary>Creates a QuantityBox with no quantity type.</summary>
    public QuantityBox()
    {
        divider.AddClass("vertical");

        Children.Add(number);
        Children.Add(divider);
        Children.Add(unitBox);

        number.ValueChanged += (_, _) => OnNumberChanged();
        unitBox.SelectionChanged += (_, _) => OnUnitPicked();
    }

    /// <summary>Raised after <see cref="Value"/> changed: when the user typed, stepped or picked another unit, or code set it.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Raised after <see cref="Unit"/> changed.</summary>
    public event EventHandler? UnitChanged;

    /// <summary>Gets or sets the kind of quantity. Setting it resets <see cref="Value"/> to zero, in the base unit of the kind.</summary>
    /// <remarks>Setting it to <see langword="null"/> empties the box.</remarks>
    public QuantityInfo? QuantityInfo
    {
        get => info;
        set
        {
            if (value?.Name == info?.Name)
                return;

            var previous = unit;

            info = value;
            units = value?.UnitInfos.Select(u => u.Value).ToList() ?? [];
            unit = value?.BaseUnitInfo.Value;
            this.value = value?.Zero.ToUnit(unit!);

            if (minimum?.QuantityInfo.Name != value?.Name)
                minimum = null;

            if (maximum?.QuantityInfo.Name != value?.Name)
                maximum = null;

            Refresh();

            if (!Equals(previous, unit))
                UnitChanged?.Invoke(this, EventArgs.Empty);

            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets or sets the kind of quantity by its UnitsNet name, such as <c>"Length"</c> or <c>"Mass"</c>.</summary>
    /// <exception cref="ArgumentException">UnitsNet has no quantity of that name.</exception>
    public string? QuantityType
    {
        get => info?.Name;
        set => QuantityInfo = value is null ? null : Find(value);
    }

    /// <summary>
    /// Gets or sets the quantity, or <see langword="null"/> while there is no <see cref="QuantityType"/>.
    /// </summary>
    /// <remarks>
    /// Without a <see cref="QuantityType"/>, setting a quantity adopts its kind. Setting <see langword="null"/> resets the
    /// number to zero. A quantity in a unit that <see cref="Units"/> does not offer is converted to the selected unit, and one
    /// outside <see cref="Minimum"/> and <see cref="Maximum"/> is limited to them.
    /// </remarks>
    /// <exception cref="ArgumentException">The quantity is of another kind than <see cref="QuantityType"/>.</exception>
    public IQuantity? Value
    {
        get => value;
        set
        {
            if (value is null)
            {
                if (info is not null)
                    Apply(info.Zero.ToUnit(unit!), unit);

                return;
            }

            if (info is null)
                QuantityInfo = value.QuantityInfo;

            if (value.QuantityInfo.Name != info!.Name)
                throw new ArgumentException($"A {value.QuantityInfo.Name} does not fit a {info.Name} box.", nameof(value));

            var shown = units.Any(u => Equals(u, value.Unit)) ? value.Unit : unit!;

            Apply(value.ToUnit(shown), shown);
        }
    }

    /// <summary>Gets or sets the selected unit.</summary>
    /// <remarks>Changing it converts the number, or keeps the digits when <see cref="ConvertOnUnitChange"/> is off.</remarks>
    /// <exception cref="ArgumentException">The unit is not one of <see cref="Units"/>.</exception>
    public Enum? Unit
    {
        get => unit;
        set
        {
            if (value is null || Equals(value, unit))
                return;

            if (!units.Any(u => Equals(u, value)))
                throw new ArgumentException($"{value} is not one of the offered units.", nameof(value));

            ChangeUnit(value);
        }
    }

    /// <summary>Gets or sets the units that the dropdown offers. By default it offers every unit of the quantity.</summary>
    /// <remarks>An empty list offers every unit again. When the selected unit is left out, the base unit is selected, or the first one.</remarks>
    /// <exception cref="InvalidOperationException">There is no <see cref="QuantityType"/> yet.</exception>
    /// <exception cref="ArgumentException">A unit is not a unit of the quantity.</exception>
    public IReadOnlyList<Enum> Units
    {
        get => units;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (info is null)
                throw new InvalidOperationException("Set QuantityType before Units.");

            if (value.Any(u => u.GetType() != info.UnitType))
                throw new ArgumentException($"Every unit must be a {info.UnitType.Name}.", nameof(value));

            units = value.Count == 0 ? info.UnitInfos.Select(u => u.Value).ToList() : [.. value];

            if (units.Any(u => Equals(u, unit)))
            {
                Refresh();

                return;
            }

            var baseUnit = info.BaseUnitInfo.Value;

            ChangeUnit(units.Any(u => Equals(u, baseUnit)) ? baseUnit : units[0]);
        }
    }

    /// <summary>Gets or sets the smallest allowed quantity, or <see langword="null"/> for no limit.</summary>
    /// <exception cref="ArgumentException">The quantity is of another kind than <see cref="QuantityType"/>.</exception>
    public IQuantity? Minimum
    {
        get => minimum;
        set
        {
            Check(value);

            minimum = value;

            Limit();
        }
    }

    /// <summary>Gets or sets the largest allowed quantity, or <see langword="null"/> for no limit.</summary>
    /// <exception cref="ArgumentException">The quantity is of another kind than <see cref="QuantityType"/>.</exception>
    public IQuantity? Maximum
    {
        get => maximum;
        set
        {
            Check(value);

            maximum = value;

            Limit();
        }
    }

    /// <summary>Gets or sets how much the Up and Down arrows change the number, in the selected unit. The default is 1.</summary>
    public double Step
    {
        get => number.Step;
        set => number.Step = value;
    }

    /// <summary>Gets or sets whether picking another unit converts the number. The default is <see langword="true"/>.</summary>
    /// <remarks>
    /// With <see langword="true"/>, 1 m becomes 100 cm and the quantity stays the same. With <see langword="false"/>, 1 m
    /// becomes 1 cm and the quantity changes.
    /// </remarks>
    public bool ConvertOnUnitChange
    {
        get => convertOnUnitChange;
        set => convertOnUnitChange = value;
    }

    /// <summary>Gets or sets the culture of the unit labels. The default is the current culture.</summary>
    public CultureInfo Culture
    {
        get => culture;
        set
        {
            culture = value ?? throw new ArgumentNullException(nameof(value));

            Refresh();
        }
    }

    /// <summary>Gets or sets how many significant digits the number shows, from 1 to 15. The default is 6.</summary>
    /// <remarks>Only the display is rounded: <see cref="Value"/> keeps its full precision.</remarks>
    public int SignificantDigits
    {
        get => significantDigits;
        set
        {
            significantDigits = Math.Clamp(value, 1, MaximumDigits);

            Refresh();
        }
    }

    /// <summary>Finds a kind of quantity by name.</summary>
    /// <param name="name">The UnitsNet name.</param>
    /// <returns>The kind.</returns>
    /// <exception cref="ArgumentException">There is no such quantity.</exception>
    static QuantityInfo Find(string name)
        => Quantity.Infos.FirstOrDefault(i => i.Name == name)
            ?? throw new ArgumentException($"UnitsNet has no quantity named '{name}'.", nameof(name));

    /// <summary>Rounds a number to a count of significant digits.</summary>
    /// <param name="x">The number.</param>
    /// <param name="digits">The count of digits.</param>
    /// <returns>The rounded number.</returns>
    static double Round(double x, int digits)
    {
        if (x == 0 || !double.IsFinite(x))
            return x;

        var decimals = digits - 1 - (int)Math.Floor(Math.Log10(Math.Abs(x)));

        if (decimals > MaximumDigits)
            return x;

        if (decimals >= 0)
            return Math.Round(x, decimals, MidpointRounding.AwayFromZero);

        var scale = Math.Pow(10, -decimals);

        return Math.Round(x / scale, MidpointRounding.AwayFromZero) * scale;
    }

    /// <summary>Checks that a limit is of the kind of this box.</summary>
    /// <param name="limit">The limit, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">The limit is of another kind.</exception>
    void Check(IQuantity? limit)
    {
        if (limit is not null && info is not null && limit.QuantityInfo.Name != info.Name)
            throw new ArgumentException($"A {limit.QuantityInfo.Name} does not fit a {info.Name} box.", nameof(limit));
    }

    /// <summary>Takes a new quantity and shows it.</summary>
    /// <param name="next">The quantity, in the unit that is to be selected.</param>
    /// <param name="shown">The unit to select.</param>
    void Apply(IQuantity next, Enum? shown)
    {
        var previous = unit;

        unit = shown;
        value = Clamp(next);

        Refresh();

        if (!Equals(previous, unit))
            UnitChanged?.Invoke(this, EventArgs.Empty);

        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Limits a quantity to <see cref="Minimum"/> and <see cref="Maximum"/>.</summary>
    /// <param name="quantity">The quantity.</param>
    /// <returns>The quantity, or the limit that it is beyond, in the same unit.</returns>
    IQuantity Clamp(IQuantity quantity)
    {
        if (minimum is not null && quantity.As(minimum.Unit) < minimum.As(minimum.Unit))
            return minimum.ToUnit(quantity.Unit);

        if (maximum is not null && quantity.As(maximum.Unit) > maximum.As(maximum.Unit))
            return maximum.ToUnit(quantity.Unit);

        return quantity;
    }

    /// <summary>Applies the limits after one of them changed.</summary>
    void Limit()
    {
        if (value is null)
            return;

        var limited = Clamp(value);

        if (limited.As(limited.Unit) != value.As(limited.Unit))
        {
            Apply(limited, unit);

            return;
        }

        Refresh();
    }

    /// <summary>Changes the unit, converting the number or keeping its digits.</summary>
    /// <param name="next">The new unit.</param>
    void ChangeUnit(Enum next)
    {
        var shown = value is null || convertOnUnitChange
            ? value?.ToUnit(next) ?? info!.Zero.ToUnit(next)
            : Quantity.From(number.Value, next);

        Apply(shown, next);
    }

    /// <summary>Reads the number after the user typed or stepped it.</summary>
    void OnNumberChanged()
    {
        if (syncing || unit is null)
            return;

        value = Quantity.From(number.Value, unit);

        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reads the unit after the user picked one.</summary>
    void OnUnitPicked()
    {
        if (syncing || unitBox.SelectedIndex < 0)
            return;

        ChangeUnit(units[unitBox.SelectedIndex]);
    }

    /// <summary>Writes the units, the limits and the number into the parts.</summary>
    void Refresh()
    {
        syncing = true;

        unitBox.Items = [.. units.Select(Label)];
        unitBox.SelectedIndex = unit is null ? -1 : units.ToList().FindIndex(u => Equals(u, unit));

        number.Minimum = minimum is null || unit is null ? double.MinValue : minimum.As(unit);
        number.Maximum = maximum is null || unit is null ? double.MaxValue : maximum.As(unit);
        number.Value = value is null || unit is null ? 0 : Round(value.As(unit), significantDigits);

        syncing = false;
    }

    /// <summary>Gets the label of a unit in the dropdown.</summary>
    /// <param name="candidate">The unit.</param>
    /// <returns>UnitsNet's default abbreviation for <see cref="Culture"/>.</returns>
    string Label(Enum candidate)
    {
        var index = Convert.ToInt32(candidate, CultureInfo.InvariantCulture);

        return UnitAbbreviationsCache.Default.GetDefaultAbbreviation(candidate.GetType(), index, culture);
    }
}

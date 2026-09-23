namespace CalcClock.Core;

/// <summary>
/// A span of time (a duration), stored internally as a total number of seconds so that
/// arithmetic is trivial and exact regardless of which units happen to be enabled for display.
/// Units only come into play when a value is entered (<see cref="FromUnitAmounts"/>) or
/// displayed (<see cref="Decompose"/>).
/// </summary>
public readonly struct ClockValue : IEquatable<ClockValue>
{
    public decimal TotalSeconds { get; }

    public ClockValue(decimal totalSeconds)
    {
        TotalSeconds = totalSeconds;
    }

    public static ClockValue Zero { get; } = new(0m);

    /// <summary>Builds a value from a set of per-unit amounts, e.g. { Hour: 2, Minute: 30 } = 2h30m.</summary>
    public static ClockValue FromUnitAmounts(IEnumerable<KeyValuePair<ClockUnit, decimal>> amounts)
    {
        decimal total = 0m;
        foreach (var (unit, amount) in amounts)
        {
            total += amount * UnitConversion.SecondsPer(unit);
        }

        return new ClockValue(total);
    }

    public static ClockValue operator +(ClockValue a, ClockValue b) => new(a.TotalSeconds + b.TotalSeconds);

    public static ClockValue operator -(ClockValue a, ClockValue b) => new(a.TotalSeconds - b.TotalSeconds);

    /// <summary>Scales a duration by a plain number (e.g. 2h × 3 = 6h).</summary>
    public static ClockValue operator *(ClockValue a, decimal scalar) => new(a.TotalSeconds * scalar);

    /// <summary>Divides a duration by a plain number (e.g. 6h ÷ 3 = 2h).</summary>
    public static ClockValue operator /(ClockValue a, decimal scalar)
    {
        if (scalar == 0m)
        {
            throw new DivideByZeroException("Cannot divide a clock value by zero.");
        }

        return new ClockValue(a.TotalSeconds / scalar);
    }

    /// <summary>
    /// Breaks this value down into the currently enabled units, largest to smallest.
    /// Every enabled unit except the smallest gets a whole-number amount; the smallest
    /// enabled unit absorbs whatever remainder is left, rounded to the nearest whole unit.
    /// </summary>
    public IReadOnlyList<(ClockUnit Unit, decimal Value)> Decompose(UnitSettings settings)
    {
        var units = settings.EnabledUnitsDescending;
        if (units.Count == 0)
        {
            throw new InvalidOperationException("At least one unit must be enabled to display a value.");
        }

        bool negative = TotalSeconds < 0m;
        decimal remaining = Math.Abs(TotalSeconds);
        var result = new List<(ClockUnit, decimal)>(units.Count);

        for (int i = 0; i < units.Count; i++)
        {
            ClockUnit unit = units[i];
            decimal secondsPerUnit = UnitConversion.SecondsPer(unit);
            bool isSmallest = i == units.Count - 1;

            decimal amount;
            if (isSmallest)
            {
                amount = Math.Round(remaining / secondsPerUnit, MidpointRounding.AwayFromZero);
            }
            else
            {
                amount = Math.Floor(remaining / secondsPerUnit);
                remaining -= amount * secondsPerUnit;
            }

            result.Add((unit, negative ? -amount : amount));
        }

        return result;
    }

    public bool Equals(ClockValue other) => TotalSeconds == other.TotalSeconds;

    public override bool Equals(object? obj) => obj is ClockValue other && Equals(other);

    public override int GetHashCode() => TotalSeconds.GetHashCode();
}

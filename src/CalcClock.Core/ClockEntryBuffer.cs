namespace CalcClock.Core;

/// <summary>
/// Digit-shift entry buffer for a segmented clock value, the same interaction a timer/stopwatch
/// picker uses: typed digits fill in from the right (the smallest enabled unit) and shift left
/// as more digits are typed. Every enabled unit except the largest holds exactly two digits;
/// the largest enabled unit is unbounded, since it must be able to absorb whatever magnitude
/// cascades down from any units that are disabled (e.g. with only Hour/Minute/Second enabled,
/// Hour has to be able to show values like 8760).
/// </summary>
public sealed class ClockEntryBuffer
{
    private const int SubUnitWidth = 2;
    private const int MaxDigits = 18;

    private string _digits = string.Empty;

    public bool IsEmpty => _digits.Length == 0;

    public void AppendDigit(int digit)
    {
        if (digit is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(digit), digit, "Must be a single digit 0-9.");
        }

        if (_digits.Length >= MaxDigits)
        {
            return;
        }

        _digits += (char)('0' + digit);
    }

    public void Backspace()
    {
        if (_digits.Length > 0)
        {
            _digits = _digits[..^1];
        }
    }

    public void Clear() => _digits = string.Empty;

    /// <summary>
    /// Splits the typed digits across the currently enabled units, largest to smallest.
    /// </summary>
    public IReadOnlyList<(ClockUnit Unit, long Value)> GetSegments(UnitSettings settings)
    {
        var units = settings.EnabledUnitsDescending;
        if (units.Count == 0)
        {
            throw new InvalidOperationException("At least one unit must be enabled to enter a value.");
        }

        int subUnitCount = units.Count - 1;
        int fixedWidth = subUnitCount * SubUnitWidth;
        string padded = _digits.Length < fixedWidth ? _digits.PadLeft(fixedWidth, '0') : _digits;

        var values = new long[units.Count];
        int cursor = padded.Length;

        // Walk from the smallest enabled unit up to (but not including) the largest, consuming
        // two digits at a time from the right.
        for (int i = units.Count - 1; i >= 1; i--)
        {
            int start = cursor - SubUnitWidth;
            values[i] = long.Parse(padded.Substring(start, SubUnitWidth));
            cursor = start;
        }

        // Whatever digits are left over on the left belong to the largest (unbounded) unit.
        string topChunk = padded[..cursor];
        values[0] = topChunk.Length == 0 ? 0L : long.Parse(topChunk);

        var result = new List<(ClockUnit, long)>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            result.Add((units[i], values[i]));
        }

        return result;
    }

    /// <summary>Converts the currently typed digits into a <see cref="ClockValue"/>.</summary>
    public ClockValue ToClockValue(UnitSettings settings)
    {
        var segments = GetSegments(settings);
        var amounts = segments.Select(s => new KeyValuePair<ClockUnit, decimal>(s.Unit, s.Value));
        return ClockValue.FromUnitAmounts(amounts);
    }
}

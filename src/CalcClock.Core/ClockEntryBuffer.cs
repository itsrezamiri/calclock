namespace CalcClock.Core;

/// <summary>
/// Digit-shift entry buffer for a segmented clock value. With no unit selected, typed digits
/// fill in from the right (the smallest enabled unit) and shift left as more digits are typed -
/// the same interaction a timer/stopwatch picker uses. Every enabled unit except the largest
/// holds exactly two digits this way; the largest enabled unit is unbounded, since it must be
/// able to absorb whatever magnitude cascades down from any units that are disabled (e.g. with
/// only Hour/Minute/Second enabled, Hour has to be able to show values like 8760).
///
/// Selecting a unit (<see cref="SelectedUnit"/>) switches to direct entry for that segment only:
/// digits shift within just that segment (same two-digit width, or unbounded if it's the largest
/// enabled unit) without touching any other segment's value.
/// </summary>
public sealed class ClockEntryBuffer
{
    private const int SubUnitWidth = 2;
    private const int MaxDigits = 18;

    private string _digits = string.Empty;

    /// <summary>
    /// Per-unit digit strings for units the user has explicitly selected and typed into directly
    /// (see <see cref="SelectedUnit"/>). A unit present here overrides whatever value the shared,
    /// cascading <see cref="_digits"/> stream would otherwise produce for it, and is edited in
    /// isolation - typing into one selected segment never shifts digits into any other segment.
    /// </summary>
    private readonly Dictionary<ClockUnit, string> _overrides = new();

    public bool IsEmpty => _digits.Length == 0 && _overrides.Count == 0;

    /// <summary>
    /// The unit segment currently selected for direct entry, or null when digits typed with no
    /// selection fall back to the default right-to-left cascading behavior.
    /// </summary>
    public ClockUnit? SelectedUnit { get; private set; }

    /// <summary>Selects a unit for direct entry, or deselects it if it's already selected.</summary>
    public void ToggleSelection(ClockUnit unit) => SelectedUnit = SelectedUnit == unit ? null : unit;

    /// <summary>Clears the selection if it points at a unit that's no longer enabled.</summary>
    public void EnsureSelectionValid(UnitSettings settings)
    {
        if (SelectedUnit is { } unit && !settings.IsEnabled(unit))
        {
            SelectedUnit = null;
        }
    }

    public void AppendDigit(int digit, UnitSettings settings)
    {
        if (digit is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(digit), digit, "Must be a single digit 0-9.");
        }

        if (SelectedUnit is { } unit && settings.IsEnabled(unit))
        {
            string current = _overrides.TryGetValue(unit, out string? existing) ? existing : string.Empty;
            if (current.Length >= MaxDigits)
            {
                return;
            }

            bool isUnbounded = settings.EnabledUnitsDescending.Count > 0 && settings.EnabledUnitsDescending[0] == unit;
            string appended = current + (char)('0' + digit);
            if (!isUnbounded && appended.Length > SubUnitWidth)
            {
                appended = appended[^SubUnitWidth..];
            }

            _overrides[unit] = appended;
            return;
        }

        if (_digits.Length >= MaxDigits)
        {
            return;
        }

        _digits += (char)('0' + digit);
    }

    public void Backspace()
    {
        if (SelectedUnit is { } unit)
        {
            if (_overrides.TryGetValue(unit, out string? existing) && existing.Length > 0)
            {
                string trimmed = existing[..^1];
                if (trimmed.Length == 0)
                {
                    _overrides.Remove(unit);
                }
                else
                {
                    _overrides[unit] = trimmed;
                }
            }

            return;
        }

        if (_digits.Length > 0)
        {
            _digits = _digits[..^1];
        }
    }

    public void Clear()
    {
        _digits = string.Empty;
        _overrides.Clear();
        SelectedUnit = null;
    }

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
            ClockUnit unit = units[i];
            long value = _overrides.TryGetValue(unit, out string? overrideDigits)
                ? (overrideDigits.Length == 0 ? 0L : long.Parse(overrideDigits))
                : values[i];
            result.Add((unit, value));
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

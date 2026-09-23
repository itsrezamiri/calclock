using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CalcClock.Core;

/// <summary>
/// Tracks which clock units are currently active. Disabling a unit does not remove its
/// magnitude from a calculation - it just means that magnitude is expressed in terms of
/// the next smaller enabled unit instead (e.g. disabling Year/Month/Day rolls everything
/// into Hour, which is why Hour/Minute/Second are enabled by default).
/// At least one unit must always remain enabled.
/// </summary>
public sealed class UnitSettings : INotifyPropertyChanged
{
    /// <summary>All units, largest to smallest. This ordering is used everywhere units are enumerated.</summary>
    public static readonly IReadOnlyList<ClockUnit> AllUnitsDescending = new[]
    {
        ClockUnit.Year, ClockUnit.Month, ClockUnit.Day, ClockUnit.Hour, ClockUnit.Minute, ClockUnit.Second,
    };

    private readonly HashSet<ClockUnit> _enabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public UnitSettings()
    {
        // Default: only Hour, Minute, Second are enabled.
        _enabled = new HashSet<ClockUnit> { ClockUnit.Hour, ClockUnit.Minute, ClockUnit.Second };
    }

    public bool IsEnabled(ClockUnit unit) => _enabled.Contains(unit);

    /// <summary>Enabled units, ordered largest to smallest.</summary>
    public IReadOnlyList<ClockUnit> EnabledUnitsDescending =>
        new ReadOnlyCollection<ClockUnit>(AllUnitsDescending.Where(IsEnabled).ToList());

    /// <summary>
    /// Enables or disables a unit. Throws if this would disable the last remaining unit,
    /// since a calculation always needs somewhere to express its value.
    /// </summary>
    public void SetEnabled(ClockUnit unit, bool enabled)
    {
        if (enabled == IsEnabled(unit))
        {
            return;
        }

        if (!enabled && _enabled.Count == 1)
        {
            throw new InvalidOperationException("At least one unit must stay enabled.");
        }

        if (enabled)
        {
            _enabled.Add(unit);
        }
        else
        {
            _enabled.Remove(unit);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnabledUnitsDescending)));
    }
}

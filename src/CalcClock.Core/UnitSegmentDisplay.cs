namespace CalcClock.Core;

/// <summary>One labeled unit segment shown in the calculator's display, e.g. "05" under "h".</summary>
public sealed class UnitSegmentDisplay
{
    public UnitSegmentDisplay(ClockUnit unit, string label, string text, bool isSelected = false)
    {
        Unit = unit;
        Label = label;
        Text = text;
        IsSelected = isSelected;
    }

    public ClockUnit Unit { get; }

    public string Label { get; }

    public string Text { get; }

    /// <summary>Whether this segment is currently selected for direct digit entry.</summary>
    public bool IsSelected { get; }
}

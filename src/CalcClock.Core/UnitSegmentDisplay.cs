namespace CalcClock.Core;

/// <summary>One labeled unit segment shown in the calculator's display, e.g. "05" under "h".</summary>
public sealed class UnitSegmentDisplay
{
    public UnitSegmentDisplay(ClockUnit unit, string label, string text)
    {
        Unit = unit;
        Label = label;
        Text = text;
    }

    public ClockUnit Unit { get; }

    public string Label { get; }

    public string Text { get; }
}

namespace CalcClock.Core;

/// <summary>
/// A unit of time a clock value can be broken down into, from largest to smallest.
/// The numeric order of the enum matters: lower values are larger units.
/// </summary>
public enum ClockUnit
{
    Year = 0,
    Month = 1,
    Day = 2,
    Hour = 3,
    Minute = 4,
    Second = 5,
}

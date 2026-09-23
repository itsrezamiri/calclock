namespace CalcClock.Core;

/// <summary>
/// Fixed conversion factors between clock units and seconds. Year and Month are not
/// fixed-length in the real calendar, so v1 uses fixed average lengths rather than
/// calendar-anchored math: 1 year = 365.25 days, 1 month = 1/12 of that (30.436875 days).
/// This keeps arithmetic simple and calendar-agnostic, at the cost of exactness for
/// individual real-world months/years.
/// </summary>
public static class UnitConversion
{
    public const decimal SecondsPerMinute = 60m;
    public const decimal SecondsPerHour = 3600m;
    public const decimal SecondsPerDay = 86400m;

    /// <summary>365.25 / 12 days, expressed in seconds.</summary>
    public const decimal SecondsPerMonth = 2629746m;

    /// <summary>365.25 days, expressed in seconds (accounts for leap years on average).</summary>
    public const decimal SecondsPerYear = 31557600m;

    public static decimal SecondsPer(ClockUnit unit) => unit switch
    {
        ClockUnit.Year => SecondsPerYear,
        ClockUnit.Month => SecondsPerMonth,
        ClockUnit.Day => SecondsPerDay,
        ClockUnit.Hour => SecondsPerHour,
        ClockUnit.Minute => SecondsPerMinute,
        ClockUnit.Second => 1m,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };
}

using CalcClock.Core;

namespace CalcClock.Core.Tests;

public class ClockValueTests
{
    [Fact]
    public void Add_CombinesTotalSeconds()
    {
        var a = new ClockValue(3600m);
        var b = new ClockValue(1800m);

        var result = a + b;

        Assert.Equal(5400m, result.TotalSeconds);
    }

    [Fact]
    public void Subtract_CanGoNegative()
    {
        var a = new ClockValue(60m);
        var b = new ClockValue(90m);

        var result = a - b;

        Assert.Equal(-30m, result.TotalSeconds);
    }

    [Fact]
    public void MultiplyByScalar_ScalesDuration()
    {
        var twoHours = new ClockValue(UnitConversion.SecondsPerHour * 2);

        var result = twoHours * 3m;

        Assert.Equal(UnitConversion.SecondsPerHour * 6, result.TotalSeconds);
    }

    [Fact]
    public void DivideByScalar_DividesDuration()
    {
        var sixHours = new ClockValue(UnitConversion.SecondsPerHour * 6);

        var result = sixHours / 3m;

        Assert.Equal(UnitConversion.SecondsPerHour * 2, result.TotalSeconds);
    }

    [Fact]
    public void DivideByZero_Throws()
    {
        var value = new ClockValue(100m);

        Assert.Throws<DivideByZeroException>(() => value / 0m);
    }

    [Fact]
    public void FromUnitAmounts_ConvertsEachUnitToSeconds()
    {
        var amounts = new Dictionary<ClockUnit, decimal>
        {
            [ClockUnit.Hour] = 2m,
            [ClockUnit.Minute] = 30m,
        };

        var value = ClockValue.FromUnitAmounts(amounts);

        Assert.Equal(2 * UnitConversion.SecondsPerHour + 30 * UnitConversion.SecondsPerMinute, value.TotalSeconds);
    }

    [Fact]
    public void Decompose_WithDefaultUnits_SplitsIntoHourMinuteSecond()
    {
        var settings = new UnitSettings(); // default: Hour, Minute, Second
        var value = new ClockValue(2 * UnitConversion.SecondsPerHour + 30 * UnitConversion.SecondsPerMinute + 15m);

        var segments = value.Decompose(settings);

        Assert.Equal(new[] { ClockUnit.Hour, ClockUnit.Minute, ClockUnit.Second }, segments.Select(s => s.Unit));
        Assert.Equal(2m, segments[0].Value);
        Assert.Equal(30m, segments[1].Value);
        Assert.Equal(15m, segments[2].Value);
    }

    [Fact]
    public void Decompose_WhenYearMonthDayDisabled_CascadesIntoHour()
    {
        // Disabling Year/Month/Day (the default) means a value of exactly one day
        // must show up entirely as hours, since Hour is the largest enabled unit.
        var settings = new UnitSettings();
        var value = new ClockValue(UnitConversion.SecondsPerDay);

        var segments = value.Decompose(settings);

        Assert.Equal(ClockUnit.Hour, segments[0].Unit);
        Assert.Equal(24m, segments[0].Value);
        Assert.Equal(0m, segments[1].Value); // Minute
        Assert.Equal(0m, segments[2].Value); // Second
    }

    [Fact]
    public void Decompose_NegativeValue_KeepsSignOnEveryUnit()
    {
        var settings = new UnitSettings();
        var value = new ClockValue(-(2 * UnitConversion.SecondsPerHour + 30 * UnitConversion.SecondsPerMinute));

        var segments = value.Decompose(settings);

        Assert.Equal(-2m, segments[0].Value);
        Assert.Equal(-30m, segments[1].Value);
        Assert.Equal(0m, segments[2].Value);
    }

    [Fact]
    public void Decompose_SmallestEnabledUnit_RoundsFractionalRemainder()
    {
        var settings = new UnitSettings();
        settings.SetEnabled(ClockUnit.Second, false); // Hour, Minute enabled; Minute is now smallest

        var value = new ClockValue(90m); // 1.5 minutes

        var segments = value.Decompose(settings);

        Assert.Equal(ClockUnit.Hour, segments[0].Unit);
        Assert.Equal(0m, segments[0].Value);
        Assert.Equal(ClockUnit.Minute, segments[1].Unit);
        Assert.Equal(2m, segments[1].Value); // rounded away from zero
    }
}

using CalcClock.Core;

namespace CalcClock.Core.Tests;

public class ClockEntryBufferTests
{
    [Fact]
    public void GetSegments_TypingDigits_ShiftsFromTheRight()
    {
        var settings = new UnitSettings(); // Hour, Minute, Second
        var buffer = new ClockEntryBuffer();

        foreach (var d in new[] { 3, 0, 4, 5 }) // types "3045"
        {
            buffer.AppendDigit(d, settings);
        }

        var segments = buffer.GetSegments(settings);

        Assert.Equal(ClockUnit.Hour, segments[0].Unit);
        Assert.Equal(0L, segments[0].Value); // no overflow into hour yet
        Assert.Equal(30L, segments[1].Value); // Minute
        Assert.Equal(45L, segments[2].Value); // Second
    }

    [Fact]
    public void GetSegments_TopEnabledUnit_IsUnboundedAndAbsorbsOverflow()
    {
        var settings = new UnitSettings(); // Hour, Minute, Second
        var buffer = new ClockEntryBuffer();

        foreach (var d in new[] { 1, 2, 3, 4, 5, 6 }) // types "123456" -> 12h 34m 56s
        {
            buffer.AppendDigit(d, settings);
        }

        var segments = buffer.GetSegments(settings);

        Assert.Equal(12L, segments[0].Value); // Hour (top, unbounded)
        Assert.Equal(34L, segments[1].Value);
        Assert.Equal(56L, segments[2].Value);
    }

    [Fact]
    public void GetSegments_EmptyBuffer_IsAllZero()
    {
        var settings = new UnitSettings();
        var buffer = new ClockEntryBuffer();

        var segments = buffer.GetSegments(settings);

        Assert.All(segments, s => Assert.Equal(0L, s.Value));
    }

    [Fact]
    public void Backspace_RemovesLastTypedDigit()
    {
        var settings = new UnitSettings();
        var buffer = new ClockEntryBuffer();
        buffer.AppendDigit(1, settings);
        buffer.AppendDigit(2, settings);

        buffer.Backspace();
        var segments = buffer.GetSegments(settings);

        Assert.Equal(1L, segments[2].Value); // Second
    }

    [Fact]
    public void ToClockValue_ConvertsSegmentsUsingConversionFactors()
    {
        var settings = new UnitSettings();
        var buffer = new ClockEntryBuffer();
        foreach (var d in new[] { 1, 0, 0 }) // "100" -> 0h 01m 00s
        {
            buffer.AppendDigit(d, settings);
        }

        var value = buffer.ToClockValue(settings);

        Assert.Equal(UnitConversion.SecondsPerMinute, value.TotalSeconds);
    }

    [Fact]
    public void GetSegments_WhenOnlyOneUnitEnabled_ThatUnitIsUnbounded()
    {
        var settings = new UnitSettings();
        settings.SetEnabled(ClockUnit.Minute, false);
        settings.SetEnabled(ClockUnit.Second, false); // only Hour left enabled
        var buffer = new ClockEntryBuffer();
        foreach (var d in new[] { 9, 9, 9 })
        {
            buffer.AppendDigit(d, settings);
        }

        var segments = buffer.GetSegments(settings);

        Assert.Single(segments);
        Assert.Equal(999L, segments[0].Value);
    }

    [Fact]
    public void SelectedUnit_TypedDigitsGoOnlyIntoThatSegment()
    {
        var settings = new UnitSettings(); // Hour, Minute, Second
        var buffer = new ClockEntryBuffer();
        buffer.AppendDigit(5, settings); // Second = 05 via the default cascading stream

        buffer.ToggleSelection(ClockUnit.Hour);
        buffer.AppendDigit(3, settings);

        var segments = buffer.GetSegments(settings);

        Assert.Equal(3L, segments[0].Value); // Hour, independently edited
        Assert.Equal(0L, segments[1].Value); // Minute, untouched
        Assert.Equal(5L, segments[2].Value); // Second, untouched by the Hour edit
    }

    [Fact]
    public void SelectedUnit_NonLargestSegment_ShiftsWithinItsOwnTwoDigitWidth()
    {
        var settings = new UnitSettings(); // Hour, Minute, Second
        var buffer = new ClockEntryBuffer();
        buffer.ToggleSelection(ClockUnit.Minute);

        foreach (var d in new[] { 1, 2, 3 }) // shifts to "23", the "1" falls off rather than carrying to Hour
        {
            buffer.AppendDigit(d, settings);
        }

        var segments = buffer.GetSegments(settings);

        Assert.Equal(0L, segments[0].Value); // Hour never receives the overflowed "1"
        Assert.Equal(23L, segments[1].Value); // Minute
    }

    [Fact]
    public void SelectedUnit_ToggleTwice_Deselects()
    {
        var buffer = new ClockEntryBuffer();
        buffer.ToggleSelection(ClockUnit.Hour);
        buffer.ToggleSelection(ClockUnit.Hour);

        Assert.Null(buffer.SelectedUnit);
    }

    [Fact]
    public void SelectedUnit_BackspaceRemovesOnlyFromThatSegment()
    {
        var settings = new UnitSettings();
        var buffer = new ClockEntryBuffer();
        buffer.ToggleSelection(ClockUnit.Minute);
        buffer.AppendDigit(4, settings);
        buffer.AppendDigit(2, settings); // Minute = "42"

        buffer.Backspace();
        var segments = buffer.GetSegments(settings);

        Assert.Equal(4L, segments[1].Value); // Minute
    }
}

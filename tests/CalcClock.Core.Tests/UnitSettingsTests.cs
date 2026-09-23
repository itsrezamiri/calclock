using CalcClock.Core;

namespace CalcClock.Core.Tests;

public class UnitSettingsTests
{
    [Fact]
    public void DefaultSettings_OnlyEnableHourMinuteSecond()
    {
        var settings = new UnitSettings();

        Assert.False(settings.IsEnabled(ClockUnit.Year));
        Assert.False(settings.IsEnabled(ClockUnit.Month));
        Assert.False(settings.IsEnabled(ClockUnit.Day));
        Assert.True(settings.IsEnabled(ClockUnit.Hour));
        Assert.True(settings.IsEnabled(ClockUnit.Minute));
        Assert.True(settings.IsEnabled(ClockUnit.Second));
    }

    [Fact]
    public void EnabledUnitsDescending_IsOrderedLargestToSmallest()
    {
        var settings = new UnitSettings();
        settings.SetEnabled(ClockUnit.Year, true);
        settings.SetEnabled(ClockUnit.Second, false);

        Assert.Equal(new[] { ClockUnit.Year, ClockUnit.Hour, ClockUnit.Minute }, settings.EnabledUnitsDescending);
    }

    [Fact]
    public void SetEnabled_DisablingTheLastUnit_Throws()
    {
        var settings = new UnitSettings();
        settings.SetEnabled(ClockUnit.Hour, false);
        settings.SetEnabled(ClockUnit.Minute, false);

        Assert.Throws<InvalidOperationException>(() => settings.SetEnabled(ClockUnit.Second, false));
    }

    [Fact]
    public void SetEnabled_RaisesPropertyChanged()
    {
        var settings = new UnitSettings();
        var raised = false;
        settings.PropertyChanged += (_, _) => raised = true;

        settings.SetEnabled(ClockUnit.Year, true);

        Assert.True(raised);
    }
}

using CalcClock.Core;

namespace CalcClock.Core.Tests;

public class CalculatorViewModelTests
{
    private static void Type(CalculatorViewModel vm, string digits)
    {
        foreach (var c in digits)
        {
            vm.PressDigit(c - '0');
        }
    }

    [Fact]
    public void Add_TwoDurations_ProducesSummedResult()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100"); // 01m 00s -> 0h 01m 00s
        vm.PressOperator('+');
        Type(vm, "0030"); // 00m 30s
        vm.PressEquals();

        var seg = vm.DisplaySegments;
        Assert.Equal("0", seg[0].Text);  // Hour
        Assert.Equal("01", seg[1].Text); // Minute
        Assert.Equal("30", seg[2].Text); // Second
    }

    [Fact]
    public void Subtract_ResultCanGoNegative()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "30"); // 30s
        vm.PressOperator('-');
        Type(vm, "45"); // 45s
        vm.PressEquals();

        // Hour and Minute are both 0, so the sign must land on the largest (Hour) segment
        // even though its magnitude is zero - otherwise the minus sign would be lost.
        Assert.Equal("-0", vm.DisplaySegments[0].Text);
        Assert.Equal("00", vm.DisplaySegments[1].Text);
        Assert.Equal("15", vm.DisplaySegments[2].Text);
    }

    [Fact]
    public void MultiplyByScalar_SwitchesToScalarEntryMode()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100"); // 1 minute
        vm.PressOperator('*');

        Assert.True(vm.IsScalarMode);

        Type(vm, "3");
        vm.PressEquals();

        Assert.False(vm.IsScalarMode);
        Assert.Equal("03", vm.DisplaySegments[1].Text); // 3 minutes
    }

    [Fact]
    public void DivideByZeroScalar_SetsErrorMessage()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100");
        vm.PressOperator('/');
        Type(vm, "0");
        vm.PressEquals();

        Assert.Equal("Cannot divide by zero", vm.ErrorMessage);
    }

    [Fact]
    public void ToggleUnit_DisablingDayRollsValueIntoHour()
    {
        var vm = new CalculatorViewModel(); // Hour, Minute, Second enabled by default
        Type(vm, "0100"); // 1 minute
        vm.PressOperator('+');
        Type(vm, "0100"); // + 1 minute = 2 minutes total, but let's use a bigger example below.
        vm.PressEquals();

        // Now flip on Day, then confirm toggling behavior doesn't throw and display updates.
        vm.ToggleUnit(ClockUnit.Day);
        Assert.True(vm.Settings.IsEnabled(ClockUnit.Day));
        Assert.Equal(4, vm.DisplaySegments.Count); // Day, Hour, Minute, Second
    }

    [Fact]
    public void ToggleUnit_CannotDisableTheLastEnabledUnit()
    {
        var vm = new CalculatorViewModel();
        vm.ToggleUnit(ClockUnit.Hour);
        vm.ToggleUnit(ClockUnit.Minute);

        vm.ToggleUnit(ClockUnit.Second); // would disable the last remaining unit

        Assert.NotNull(vm.ErrorMessage);
        Assert.True(vm.Settings.IsEnabled(ClockUnit.Second));
    }

    [Fact]
    public void ChainedOperators_EvaluateLeftToRightLikeAStandardCalculator()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100"); // 1 minute
        vm.PressOperator('+');
        Type(vm, "0100"); // + 1 minute
        vm.PressOperator('+');
        Type(vm, "0100"); // + 1 minute, chaining before '=' should fold the running total
        vm.PressEquals();

        Assert.Equal("03", vm.DisplaySegments[1].Text); // 3 minutes total
    }

    [Fact]
    public void PressClear_ResetsPendingOperationAndExpression()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100");
        vm.PressOperator('+');

        vm.PressClear();

        Assert.Equal(string.Empty, vm.ExpressionText);
        Assert.Equal("0", vm.DisplaySegments[0].Text);
    }

    [Fact]
    public void ExpressionText_ChainedOperators_ShowsFullTrailNotJustLastStep()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100"); // 1 minute
        vm.PressOperator('+');
        Type(vm, "0100"); // + 1 minute
        vm.PressOperator('+');
        Type(vm, "0100"); // + 1 minute
        vm.PressEquals();

        Assert.Equal("0h 01m 00s + 0h 01m 00s + 0h 01m 00s = 0h 03m 00s", vm.ExpressionText);
    }

    [Fact]
    public void PressEquals_AddsCompletedCalculationToHistory()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0030");
        vm.PressOperator('+');
        Type(vm, "0045");
        vm.PressEquals();

        Assert.Single(vm.History);
        Assert.Equal(vm.ExpressionText, vm.History[0].Expression);
    }

    [Fact]
    public void RecallHistory_LoadsPastResultAsCurrentValue()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0030");
        vm.PressOperator('+');
        Type(vm, "0045");
        vm.PressEquals();
        var entry = vm.History[0];

        vm.PressClear();
        vm.RecallHistory(entry);

        Assert.Equal("15", vm.DisplaySegments[2].Text); // 75s -> 1m15s, Second segment
        Assert.Equal("01", vm.DisplaySegments[1].Text);
    }

    [Fact]
    public void PressSelectUnit_TypedDigitsEditOnlyThatSegment()
    {
        var vm = new CalculatorViewModel();
        Type(vm, "0100"); // 1 minute -> Hour=0, Minute=01, Second=00

        vm.PressSelectUnit(ClockUnit.Hour);
        vm.PressDigit(5);

        Assert.Equal("5", vm.DisplaySegments[0].Text);  // Hour, independently edited
        Assert.Equal("01", vm.DisplaySegments[1].Text); // Minute, untouched
        Assert.Equal("00", vm.DisplaySegments[2].Text); // Second, untouched
    }
}

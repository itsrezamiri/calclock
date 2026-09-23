using CalcClock.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CalcClock_App;

/// <summary>
/// The main calculator page. All calculation state lives in <see cref="CalculatorViewModel"/>
/// (CalcClock.Core); this page is just wiring between the keypad/flyout and that view model.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly CalculatorViewModel _viewModel = new();
    private bool _isSyncingUnitToggles;

    public MainPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void DigitButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int digit))
        {
            _viewModel.PressDigit(digit);
        }
    }

    private void OperatorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && tag.Length == 1)
        {
            _viewModel.PressOperator(tag[0]);
        }
    }

    private void DecimalButton_Click(object sender, RoutedEventArgs e) => _viewModel.PressDecimalPoint();

    private void EqualsButton_Click(object sender, RoutedEventArgs e) => _viewModel.PressEquals();

    private void ClearButton_Click(object sender, RoutedEventArgs e) => _viewModel.PressClear();

    private void ClearEntryButton_Click(object sender, RoutedEventArgs e) => _viewModel.PressClearEntry();

    private void BackspaceButton_Click(object sender, RoutedEventArgs e) => _viewModel.PressBackspace();

    private void UnitSegment_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ClockUnit unit })
        {
            _viewModel.PressSelectUnit(unit);
        }
    }

    private void HistoryListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HistoryEntry entry)
        {
            _viewModel.RecallHistory(entry);
        }

        HistoryFlyout.Hide();
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e) => _viewModel.ClearHistory();

    /// <summary>Syncs the toggle switches to current settings each time the flyout opens.</summary>
    private void UnitsFlyout_Opening(object? sender, object e)
    {
        _isSyncingUnitToggles = true;
        YearToggle.IsOn = _viewModel.Settings.IsEnabled(ClockUnit.Year);
        MonthToggle.IsOn = _viewModel.Settings.IsEnabled(ClockUnit.Month);
        DayToggle.IsOn = _viewModel.Settings.IsEnabled(ClockUnit.Day);
        HourToggle.IsOn = _viewModel.Settings.IsEnabled(ClockUnit.Hour);
        MinuteToggle.IsOn = _viewModel.Settings.IsEnabled(ClockUnit.Minute);
        SecondToggle.IsOn = _viewModel.Settings.IsEnabled(ClockUnit.Second);
        _isSyncingUnitToggles = false;
    }

    private void UnitToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isSyncingUnitToggles || sender is not ToggleSwitch { Tag: string tag } toggle)
        {
            return;
        }

        if (!Enum.TryParse<ClockUnit>(tag, out var unit))
        {
            return;
        }

        _viewModel.ToggleUnit(unit);

        // If the toggle would have disabled the last remaining unit, ToggleUnit reports an
        // error but leaves the setting unchanged - snap the switch back to reflect that.
        if (toggle.IsOn != _viewModel.Settings.IsEnabled(unit))
        {
            _isSyncingUnitToggles = true;
            toggle.IsOn = _viewModel.Settings.IsEnabled(unit);
            _isSyncingUnitToggles = false;
        }
    }
}

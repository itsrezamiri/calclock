using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace CalcClock.Core;

/// <summary>
/// Drives the calculator's state machine: digit/operator entry, the pending operation,
/// and formatting the display. UI-framework agnostic on purpose, so it can be unit tested
/// without spinning up WinUI, and so the WinUI page can stay a thin binding layer.
/// </summary>
public sealed class CalculatorViewModel : INotifyPropertyChanged
{
    private const int MaxHistoryEntries = 100;

    private readonly ClockEntryBuffer _buffer = new();
    private readonly List<string> _expressionTokens = new();

    private string _scalarDigits = string.Empty;
    private ClockValue? _pendingOperand;
    private char? _pendingOperator;
    private ClockValue? _lastResult;
    private bool _showResultWhenEntryEmpty;

    private string _expressionText = string.Empty;
    private string? _errorMessage;
    private CalculatorEntryMode _entryMode = CalculatorEntryMode.Segmented;

    public event PropertyChangedEventHandler? PropertyChanged;

    public CalculatorViewModel(UnitSettings? settings = null)
    {
        Settings = settings ?? new UnitSettings();
        Settings.PropertyChanged += (_, _) =>
        {
            _buffer.EnsureSelectionValid(Settings);
            RefreshDisplay();
        };
        RefreshDisplay();
    }

    public UnitSettings Settings { get; }

    /// <summary>Live segmented display, one entry per enabled unit, largest to smallest.</summary>
    public ObservableCollection<UnitSegmentDisplay> DisplaySegments { get; } = new();

    /// <summary>Completed calculations for this session, newest first. Capped at <see cref="MaxHistoryEntries"/>.</summary>
    public ObservableCollection<HistoryEntry> History { get; } = new();

    public bool IsScalarMode => _entryMode == CalculatorEntryMode.Scalar;

    public string ScalarText { get; private set; } = "0";

    public string ExpressionText
    {
        get => _expressionText;
        private set => SetField(ref _expressionText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public void PressDigit(int digit)
    {
        ClearError();

        // Starting to type a brand new value (no chained operator pending) drops the previous
        // calculation's expression trail rather than leaving it stranded on screen.
        bool isFreshEntry = _pendingOperator is null
            && (_entryMode == CalculatorEntryMode.Scalar ? _scalarDigits.Length == 0 : _buffer.IsEmpty);
        if (isFreshEntry && _expressionTokens.Count > 0)
        {
            _expressionTokens.Clear();
            ExpressionText = string.Empty;
        }

        if (_entryMode == CalculatorEntryMode.Scalar)
        {
            if (_scalarDigits.Length < 15)
            {
                _scalarDigits += digit.ToString(CultureInfo.InvariantCulture);
            }
        }
        else
        {
            _buffer.AppendDigit(digit, Settings);
        }

        _showResultWhenEntryEmpty = false;
        RefreshDisplay();
    }

    /// <summary>
    /// Selects (or deselects, if already selected) a unit segment for direct entry. While a
    /// segment is selected, digits typed shift within just that segment - see
    /// <see cref="ClockEntryBuffer"/>. Has no effect in scalar entry mode, since segments aren't
    /// shown there.
    /// </summary>
    public void PressSelectUnit(ClockUnit unit)
    {
        if (_entryMode == CalculatorEntryMode.Scalar || _errorMessage != null || !Settings.IsEnabled(unit))
        {
            return;
        }

        _buffer.ToggleSelection(unit);
        RefreshDisplay();
    }

    public void PressDecimalPoint()
    {
        if (_entryMode != CalculatorEntryMode.Scalar || _errorMessage != null)
        {
            return;
        }

        if (_scalarDigits.Length == 0)
        {
            _scalarDigits = "0.";
        }
        else if (!_scalarDigits.Contains('.'))
        {
            _scalarDigits += ".";
        }

        RefreshDisplay();
    }

    public void PressBackspace()
    {
        ClearError();

        if (_entryMode == CalculatorEntryMode.Scalar)
        {
            if (_scalarDigits.Length > 0)
            {
                _scalarDigits = _scalarDigits[..^1];
            }
        }
        else
        {
            _buffer.Backspace();
        }

        RefreshDisplay();
    }

    /// <summary>Clears just the value currently being typed, leaving any pending operation intact.</summary>
    public void PressClearEntry()
    {
        ClearError();
        _buffer.Clear();
        _scalarDigits = string.Empty;
        _showResultWhenEntryEmpty = false;
        RefreshDisplay();
    }

    /// <summary>Full reset: clears the entry, any pending operation, and the expression history.</summary>
    public void PressClear()
    {
        _buffer.Clear();
        _scalarDigits = string.Empty;
        _pendingOperand = null;
        _pendingOperator = null;
        _lastResult = null;
        _showResultWhenEntryEmpty = false;
        _entryMode = CalculatorEntryMode.Segmented;
        _expressionTokens.Clear();
        ExpressionText = string.Empty;
        ClearError();
        RefreshDisplay();
    }

    /// <summary>Applies +, -, * or / as the next pending operator.</summary>
    public void PressOperator(char op)
    {
        if (op is not ('+' or '-' or '*' or '/'))
        {
            throw new ArgumentOutOfRangeException(nameof(op), op, "Must be one of + - * /.");
        }

        if (_errorMessage != null)
        {
            return;
        }

        // Whether the user typed a new operand since the last operator/equals press, as opposed
        // to just changing their mind about which operator to apply.
        bool hasNewInput = _entryMode == CalculatorEntryMode.Scalar ? _scalarDigits.Length > 0 : !_buffer.IsEmpty;
        bool isFreshChain = _pendingOperator is null;

        ClockValue result;
        try
        {
            result = ResolveCurrentResult();
        }
        catch (DivideByZeroException)
        {
            EnterDivideByZeroError();
            return;
        }

        if (isFreshChain)
        {
            // A fresh chain starts from whatever is currently entered (or the previous result,
            // per ResolveCurrentResult), discarding any earlier finished calculation's trail.
            _expressionTokens.Clear();
            _expressionTokens.Add(FormatClockValue(result));
            _expressionTokens.Add(OperatorSymbol(op));
        }
        else if (hasNewInput)
        {
            string operandText = _entryMode == CalculatorEntryMode.Scalar
                ? FormatScalarDigits(_scalarDigits)
                : FormatClockValue(CurrentSegmentedEntryValue());
            _expressionTokens.Add(operandText);
            _expressionTokens.Add(OperatorSymbol(op));
        }
        else
        {
            // No new operand was typed - just swap the pending operator in place.
            _expressionTokens[^1] = OperatorSymbol(op);
        }

        ExpressionText = string.Join(" ", _expressionTokens);

        _pendingOperand = result;
        _pendingOperator = op;
        _lastResult = result;
        _showResultWhenEntryEmpty = true;

        _buffer.Clear();
        _scalarDigits = string.Empty;
        _entryMode = op is '*' or '/' ? CalculatorEntryMode.Scalar : CalculatorEntryMode.Segmented;

        RefreshDisplay();
    }

    public void PressEquals()
    {
        if (_pendingOperator is null || _errorMessage != null)
        {
            return;
        }

        string operand2Text = _entryMode == CalculatorEntryMode.Scalar
            ? FormatScalarDigits(_scalarDigits)
            : FormatClockValue(CurrentSegmentedEntryValue());

        ClockValue result;
        try
        {
            result = ResolveCurrentResult();
        }
        catch (DivideByZeroException)
        {
            EnterDivideByZeroError();
            return;
        }

        _expressionTokens.Add(operand2Text);
        _expressionTokens.Add("=");
        _expressionTokens.Add(FormatClockValue(result));
        ExpressionText = string.Join(" ", _expressionTokens);

        History.Insert(0, new HistoryEntry(ExpressionText, result));
        while (History.Count > MaxHistoryEntries)
        {
            History.RemoveAt(History.Count - 1);
        }

        _pendingOperand = null;
        _pendingOperator = null;
        _lastResult = result;
        _showResultWhenEntryEmpty = true;

        _buffer.Clear();
        _scalarDigits = string.Empty;
        _entryMode = CalculatorEntryMode.Segmented;

        RefreshDisplay();
    }

    /// <summary>Recalls a past calculation's result as the current value, ready for further operations.</summary>
    public void RecallHistory(HistoryEntry entry)
    {
        ClearError();

        _expressionTokens.Clear();
        _buffer.Clear();
        _scalarDigits = string.Empty;
        _pendingOperand = null;
        _pendingOperator = null;
        _lastResult = entry.Result;
        _showResultWhenEntryEmpty = true;
        _entryMode = CalculatorEntryMode.Segmented;
        ExpressionText = entry.Expression;

        RefreshDisplay();
    }

    public void ClearHistory() => History.Clear();

    public void ToggleUnit(ClockUnit unit)
    {
        try
        {
            Settings.SetEnabled(unit, !Settings.IsEnabled(unit));
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Resolves the value of whatever is currently being entered, combined with any pending operation.</summary>
    private ClockValue ResolveCurrentResult()
    {
        if (_entryMode == CalculatorEntryMode.Scalar)
        {
            // Scalar mode is only ever entered via * or /, so a pending operand/operator always exists.
            decimal scalar = ParseScalarDigits(_scalarDigits);
            return ApplyScalarOperator(_pendingOperator!.Value, _pendingOperand!.Value, scalar);
        }

        ClockValue entered = CurrentSegmentedEntryValue();

        if (_pendingOperator is null)
        {
            return entered;
        }

        // If we're back in Segmented mode with a pending operator, it must be + or -.
        return ApplyDurationOperator(_pendingOperator.Value, _pendingOperand!.Value, entered);
    }

    private ClockValue CurrentSegmentedEntryValue()
    {
        if (!_buffer.IsEmpty)
        {
            return _buffer.ToClockValue(Settings);
        }

        return _showResultWhenEntryEmpty && _lastResult.HasValue ? _lastResult.Value : ClockValue.Zero;
    }

    private static ClockValue ApplyDurationOperator(char op, ClockValue a, ClockValue b) => op switch
    {
        '+' => a + b,
        '-' => a - b,
        _ => throw new InvalidOperationException($"'{op}' is not a duration operator."),
    };

    private static ClockValue ApplyScalarOperator(char op, ClockValue a, decimal scalar) => op switch
    {
        '*' => a * scalar,
        '/' => a / scalar,
        _ => throw new InvalidOperationException($"'{op}' is not a scalar operator."),
    };

    private void EnterDivideByZeroError()
    {
        ErrorMessage = "Cannot divide by zero";
        _pendingOperand = null;
        _pendingOperator = null;
        _lastResult = null;
        _showResultWhenEntryEmpty = false;
        _buffer.Clear();
        _scalarDigits = string.Empty;
        _entryMode = CalculatorEntryMode.Segmented;
        RefreshDisplay();
    }

    private void ClearError() => ErrorMessage = null;

    private void RefreshDisplay()
    {
        if (_entryMode == CalculatorEntryMode.Scalar)
        {
            ScalarText = FormatScalarDigits(_scalarDigits);
        }
        else
        {
            ClockValue sourceValue = _showResultWhenEntryEmpty && _lastResult.HasValue ? _lastResult.Value : ClockValue.Zero;
            bool isNegative = _buffer.IsEmpty && sourceValue.TotalSeconds < 0m;

            IReadOnlyList<(ClockUnit Unit, decimal Value)> segments = _buffer.IsEmpty
                ? sourceValue.Decompose(Settings)
                : _buffer.GetSegments(Settings).Select(s => (s.Unit, (decimal)s.Value)).ToList();

            // Decimal has no negative zero, so a negative value whose largest enabled unit happens to
            // be zero (e.g. -15s shown as Hour/Minute/Second) would otherwise print with no sign at all.
            // Instead, show every unit's magnitude and put the single "-" on the largest unit only.
            DisplaySegments.Clear();
            for (int i = 0; i < segments.Count; i++)
            {
                var (unit, value) = segments[i];
                bool isLargest = i == 0;
                string magnitude = Math.Abs(value).ToString(isLargest ? "0" : "00", CultureInfo.InvariantCulture);
                string text = isLargest && isNegative ? "-" + magnitude : magnitude;
                DisplaySegments.Add(new UnitSegmentDisplay(unit, UnitLabel(unit), text, unit == _buffer.SelectedUnit));
            }
        }

        OnPropertyChanged(nameof(IsScalarMode));
        OnPropertyChanged(nameof(ScalarText));
    }

    /// <summary>Compact "3h 05m 09s"-style summary used for the expression history line, using the enabled units.</summary>
    private string FormatClockValue(ClockValue value)
    {
        bool isNegative = value.TotalSeconds < 0m;
        var segments = value.Decompose(Settings);
        string body = string.Join(" ", segments.Select((s, i) =>
        {
            string magnitude = Math.Abs(s.Value).ToString(i == 0 ? "0" : "00", CultureInfo.InvariantCulture);
            return magnitude + UnitLabel(s.Unit);
        }));
        return isNegative ? "-" + body : body;
    }

    private static string FormatScalarDigits(string digits) => digits.Length == 0 ? "0" : digits;

    private static decimal ParseScalarDigits(string digits)
    {
        if (digits.Length == 0 || digits == ".")
        {
            return 0m;
        }

        return decimal.Parse(digits, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    private static string UnitLabel(ClockUnit unit) => unit switch
    {
        ClockUnit.Year => "y",
        ClockUnit.Month => "mo",
        ClockUnit.Day => "d",
        ClockUnit.Hour => "h",
        ClockUnit.Minute => "m",
        ClockUnit.Second => "s",
        _ => unit.ToString(),
    };

    private static string OperatorSymbol(char op) => op switch
    {
        '+' => "+",
        '-' => "−",
        '*' => "×",
        '/' => "÷",
        _ => op.ToString(),
    };

    private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
        }
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

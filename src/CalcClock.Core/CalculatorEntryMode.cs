namespace CalcClock.Core;

/// <summary>
/// What the current operand being typed represents: a full segmented clock value (for + and -),
/// or a plain scalar number (for × and ÷, since scaling a duration by a duration is undefined).
/// </summary>
public enum CalculatorEntryMode
{
    Segmented,
    Scalar,
}

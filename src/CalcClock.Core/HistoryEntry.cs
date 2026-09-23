namespace CalcClock.Core;

/// <summary>One completed calculation kept in the session history, e.g. "01m + 01m = 02m".</summary>
public sealed record HistoryEntry(string Expression, ClockValue Result);

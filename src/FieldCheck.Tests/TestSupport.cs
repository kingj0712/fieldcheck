using FieldCheck.Core.Utilities;

namespace FieldCheck.Tests;

/// <summary>A controllable clock so tests can assert exact timestamp behavior.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTime start) => Now = start;

    public DateTime Now { get; set; }

    public void Advance(TimeSpan amount) => Now = Now.Add(amount);

    public static FakeClock Default => new(new DateTime(2026, 6, 2, 8, 0, 0));
}

/// <summary>Deterministic ids (checklist-001, item-001, ...) so tests can assert on ordering.</summary>
public sealed class SequentialIdGenerator : IIdGenerator
{
    private readonly Dictionary<string, int> _counters = new();

    public string NewId(string prefix)
    {
        _counters.TryGetValue(prefix, out var n);
        n++;
        _counters[prefix] = n;
        return $"{prefix}-{n:D3}";
    }
}

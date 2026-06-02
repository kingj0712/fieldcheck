namespace FieldCheck.Core.Utilities;

/// <summary>
/// I inject the clock everywhere a timestamp is stamped so that tests can assert exact
/// CreatedAt/UpdatedAt behavior without depending on the wall clock.
/// </summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>The real clock used by the running app. I use local time to match the data model.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTime Now => DateTime.Now;
}

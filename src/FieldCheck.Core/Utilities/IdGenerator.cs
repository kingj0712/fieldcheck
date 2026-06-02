namespace FieldCheck.Core.Utilities;

/// <summary>
/// Produces unique ids with a readable prefix, e.g. "item-3f9c1a2b...". I keep this behind an
/// interface so tests can substitute a deterministic generator when they need stable ids.
/// </summary>
public interface IIdGenerator
{
    string NewId(string prefix);
}

/// <summary>Default generator backed by <see cref="Guid"/>, so collisions are not a concern.</summary>
public sealed class IdGenerator : IIdGenerator
{
    public static readonly IdGenerator Instance = new();

    public string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}

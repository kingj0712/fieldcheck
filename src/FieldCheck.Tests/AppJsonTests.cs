using System.Text.Json;
using FieldCheck.Core.Models;
using FieldCheck.Core.Utilities;
using Xunit;

namespace FieldCheck.Tests;

public class AppJsonTests
{
    private static ChecklistItem DeserializeWithCompletedAt(string completedAtToken)
    {
        var json = "{\"id\":\"i\",\"text\":\"t\",\"section\":\"General\",\"notes\":\"\",\"order\":1," +
                   "\"completed\":false,\"completedAt\":" + completedAtToken + "," +
                   "\"createdAt\":\"2026-06-02T08:00:00\",\"updatedAt\":\"2026-06-02T08:00:00\"}";
        return JsonSerializer.Deserialize<ChecklistItem>(json, AppJson.Options)!;
    }

    [Fact]
    public void CompletedAt_ExplicitNull_StaysNull()
        => Assert.Null(DeserializeWithCompletedAt("null").CompletedAt);

    [Fact]
    public void CompletedAt_EmptyString_DeserializesToNull()
        => Assert.Null(DeserializeWithCompletedAt("\"\"").CompletedAt);

    [Fact]
    public void CompletedAt_UnparseableString_DeserializesToNull()
        => Assert.Null(DeserializeWithCompletedAt("\"not-a-date\"").CompletedAt);

    [Fact]
    public void CompletedAt_ValidString_Parses()
        => Assert.Equal(new DateTime(2026, 6, 2, 9, 10, 0), DeserializeWithCompletedAt("\"2026-06-02T09:10:00\"").CompletedAt);

    [Fact]
    public void RequiredDates_StillParse()
    {
        var item = DeserializeWithCompletedAt("null");
        Assert.Equal(new DateTime(2026, 6, 2, 8, 0, 0), item.CreatedAt);
        Assert.Equal(new DateTime(2026, 6, 2, 8, 0, 0), item.UpdatedAt);
    }

    [Fact]
    public void NullCompletedAt_SerializesAsJsonNull()
    {
        var item = new ChecklistItem
        {
            Id = "i", Text = "t",
            CreatedAt = new DateTime(2026, 6, 2, 8, 0, 0),
            UpdatedAt = new DateTime(2026, 6, 2, 8, 0, 0)
        };
        var json = JsonSerializer.Serialize(item, AppJson.Options);
        Assert.Contains("\"completedAt\": null", json);
    }

    [Fact]
    public void CompletedAt_RoundTrips()
    {
        var item = new ChecklistItem
        {
            Id = "i", Text = "t", Completed = true,
            CompletedAt = new DateTime(2026, 6, 2, 9, 10, 0),
            CreatedAt = new DateTime(2026, 6, 2, 8, 0, 0),
            UpdatedAt = new DateTime(2026, 6, 2, 8, 0, 0)
        };
        var json = JsonSerializer.Serialize(item, AppJson.Options);
        var restored = JsonSerializer.Deserialize<ChecklistItem>(json, AppJson.Options)!;
        Assert.Equal(item.CompletedAt, restored.CompletedAt);
    }
}

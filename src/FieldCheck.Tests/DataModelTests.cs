using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using FieldCheck.Core.Utilities;
using Xunit;

namespace FieldCheck.Tests;

public class DataModelTests
{
    [Fact]
    public void NewIds_AreUnique()
    {
        var generator = new IdGenerator();
        var ids = new HashSet<string>();
        for (int i = 0; i < 5000; i++)
        {
            Assert.True(ids.Add(generator.NewId("item")), "Generated a duplicate id.");
            Assert.True(ids.Add(generator.NewId("project")), "Generated a duplicate id.");
        }
    }

    [Fact]
    public void UpdatedAt_Changes_While_CreatedAt_Stays_Stable()
    {
        var clock = FakeClock.Default;
        var ids = new SequentialIdGenerator();
        var item = ChecklistService.CreateItem("Verify", null, null, null, clock, ids);
        var createdAt = item.CreatedAt;
        var originalUpdatedAt = item.UpdatedAt;

        clock.Advance(TimeSpan.FromMinutes(10));
        ChecklistService.EditItem(item, "Verify edited", null, null, null, clock);

        Assert.Equal(createdAt, item.CreatedAt);
        Assert.True(item.UpdatedAt > originalUpdatedAt);
        Assert.Equal(clock.Now, item.UpdatedAt);
    }

    [Fact]
    public void Order_IsNormalized_AfterImport()
    {
        var state = new AppState();
        CsvImportService.Import("checklist_name,item_text\nAHU,One\nAHU,Two\nAHU,Three",
            "x.csv", state, FakeClock.Default, new SequentialIdGenerator());

        var checklist = state.Projects.Single().Checklists.Single();
        Assert.Equal(new[] { 1, 2, 3 }, checklist.Items.Select(i => i.Order).ToArray());
    }

    [Fact]
    public void Order_IsNormalized_AfterDelete()
    {
        var clock = FakeClock.Default;
        var ids = new SequentialIdGenerator();
        var checklist = ChecklistService.CreateChecklist("AHU", clock, ids);
        foreach (var t in new[] { "One", "Two", "Three", "Four" })
            ChecklistService.AddItem(checklist, ChecklistService.CreateItem(t, null, null, null, clock, ids), clock);

        ChecklistService.DeleteItem(checklist, checklist.Items[1].Id, clock);

        Assert.Equal(new[] { 1, 2, 3 }, checklist.Items.Select(i => i.Order).ToArray());
        Assert.Equal(new[] { "One", "Three", "Four" }, checklist.Items.Select(i => i.Text).ToArray());
    }

    [Fact]
    public void Order_IsContiguous_AfterReorder()
    {
        var clock = FakeClock.Default;
        var ids = new SequentialIdGenerator();
        var checklist = ChecklistService.CreateChecklist("AHU", clock, ids);
        foreach (var t in new[] { "One", "Two", "Three" })
            ChecklistService.AddItem(checklist, ChecklistService.CreateItem(t, null, null, null, clock, ids), clock);

        ChecklistService.ReorderItems(checklist.Items, 0, 2);
        Assert.Equal(new[] { 1, 2, 3 }, checklist.Items.Select(i => i.Order).ToArray());
    }
}

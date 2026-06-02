using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Xunit;

namespace FieldCheck.Tests;

public class ChecklistServiceTests
{
    private readonly FakeClock _clock = FakeClock.Default;
    private readonly SequentialIdGenerator _ids = new();

    private Checklist NewChecklistWith(params string[] itemTexts)
    {
        var checklist = ChecklistService.CreateChecklist("Test", _clock, _ids);
        foreach (var text in itemTexts)
            ChecklistService.AddItem(checklist, ChecklistService.CreateItem(text, null, null, null, _clock, _ids), _clock);
        return checklist;
    }

    [Fact]
    public void CreateChecklist_SetsFieldsAndTimestamps()
    {
        var checklist = ChecklistService.CreateChecklist("  AHU_1  ", _clock, _ids);
        Assert.Equal("AHU_1", checklist.Name);
        Assert.Equal(_clock.Now, checklist.CreatedAt);
        Assert.Equal(_clock.Now, checklist.UpdatedAt);
        Assert.StartsWith("checklist-", checklist.Id);
    }

    [Fact]
    public void CreateChecklist_BlankName_Throws()
        => Assert.Throws<ArgumentException>(() => ChecklistService.CreateChecklist("   ", _clock, _ids));

    [Fact]
    public void CreateItem_BlankText_Throws()
        => Assert.Throws<ArgumentException>(() => ChecklistService.CreateItem("  ", null, null, null, _clock, _ids));

    [Fact]
    public void CreateItem_BlankSection_DefaultsToGeneral()
        => Assert.Equal("General", ChecklistService.CreateItem("Verify", "  ", null, null, _clock, _ids).Section);

    [Fact]
    public void CreateItem_NormalizesTags()
    {
        var item = ChecklistService.CreateItem("Verify", "Fan", null, new[] { " AHU-1 ", "BAS", "bas", "" }, _clock, _ids);
        Assert.Equal(new[] { "AHU-1", "BAS" }, item.Tags);
    }

    [Fact]
    public void Rename_UpdatesNameAndTimestamp()
    {
        var checklist = NewChecklistWith();
        var created = checklist.CreatedAt;
        _clock.Advance(TimeSpan.FromMinutes(5));
        ChecklistService.Rename(checklist, "Renamed", _clock);
        Assert.Equal("Renamed", checklist.Name);
        Assert.Equal(_clock.Now, checklist.UpdatedAt);
        Assert.Equal(created, checklist.CreatedAt);
    }

    [Fact]
    public void Rename_BlankName_Throws()
    {
        var checklist = NewChecklistWith();
        Assert.Throws<ArgumentException>(() => ChecklistService.Rename(checklist, " ", _clock));
    }

    [Fact]
    public void Duplicate_CreatesIndependentCopyWithUniqueName()
    {
        var source = NewChecklistWith("One", "Two");
        source.Items[0].Tags.Add("AHU-1");
        ChecklistService.SetItemCompleted(source.Items[0], true, _clock);

        var copy = ChecklistService.Duplicate(source, new[] { source.Name }, _clock, _ids);

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Test Copy", copy.Name);
        Assert.Equal(2, copy.Items.Count);
        Assert.All(copy.Items, ci => Assert.DoesNotContain(source.Items, si => si.Id == ci.Id));
        Assert.True(copy.Items[0].Completed);
        Assert.Equal(new[] { "AHU-1" }, copy.Items[0].Tags);

        copy.Items[0].Tags.Add("New"); // independent tag list
        Assert.Single(source.Items[0].Tags);
    }

    [Fact]
    public void ResetChecklist_ClearsCompletionState()
    {
        var checklist = NewChecklistWith("One", "Two");
        ChecklistService.SetItemCompleted(checklist.Items[0], true, _clock);
        ChecklistService.SetItemCompleted(checklist.Items[1], true, _clock);
        ChecklistService.ResetChecklist(checklist, _clock);
        Assert.All(checklist.Items, i => Assert.False(i.Completed));
        Assert.All(checklist.Items, i => Assert.Null(i.CompletedAt));
    }

    [Fact]
    public void AddItem_AppendsAndAssignsOrder()
    {
        var checklist = NewChecklistWith("One");
        _clock.Advance(TimeSpan.FromMinutes(1));
        ChecklistService.AddItem(checklist, ChecklistService.CreateItem("Two", null, null, null, _clock, _ids), _clock);
        Assert.Equal(new[] { 1, 2 }, checklist.Items.Select(i => i.Order).ToArray());
    }

    [Fact]
    public void EditItem_UpdatesFieldsTagsAndTimestamp()
    {
        var checklist = NewChecklistWith("One");
        var item = checklist.Items[0];
        var created = item.CreatedAt;
        _clock.Advance(TimeSpan.FromMinutes(2));
        ChecklistService.EditItem(item, "Edited", "Fan", "Some notes", new[] { "AHU-1", "AHU-1" }, _clock);
        Assert.Equal("Edited", item.Text);
        Assert.Equal("Fan", item.Section);
        Assert.Equal("Some notes", item.Notes);
        Assert.Equal(new[] { "AHU-1" }, item.Tags);
        Assert.Equal(_clock.Now, item.UpdatedAt);
        Assert.Equal(created, item.CreatedAt);
    }

    [Fact]
    public void DeleteItem_RemovesAndRenumbers()
    {
        var checklist = NewChecklistWith("One", "Two", "Three");
        ChecklistService.DeleteItem(checklist, checklist.Items[1].Id, _clock);
        Assert.Equal(new[] { 1, 2 }, checklist.Items.Select(i => i.Order).ToArray());
        Assert.Equal(new[] { "One", "Three" }, checklist.Items.Select(i => i.Text).ToArray());
    }

    [Fact]
    public void ReorderItems_UpdatesOrder()
    {
        var checklist = NewChecklistWith("One", "Two", "Three");
        ChecklistService.ReorderItems(checklist.Items, 2, 0);
        Assert.Equal(new[] { "Three", "One", "Two" }, checklist.Items.Select(i => i.Text).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, checklist.Items.Select(i => i.Order).ToArray());
    }

    [Fact]
    public void SetItemCompleted_StampsAndClearsCompletedAt()
    {
        var checklist = NewChecklistWith("One");
        var item = checklist.Items[0];
        _clock.Advance(TimeSpan.FromMinutes(3));
        ChecklistService.SetItemCompleted(item, true, _clock);
        Assert.True(item.Completed);
        Assert.Equal(_clock.Now, item.CompletedAt);
        ChecklistService.SetItemCompleted(item, false, _clock);
        Assert.False(item.Completed);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public void UncheckingItem_RestoresOriginalPosition()
    {
        var checklist = NewChecklistWith("One", "Two", "Three");
        var two = checklist.Items[1];
        ChecklistService.SetItemCompleted(two, true, _clock);
        Assert.Equal(new[] { "One", "Three" }, ChecklistService.OpenItems(checklist).Select(i => i.Text).ToArray());
        ChecklistService.SetItemCompleted(two, false, _clock);
        Assert.Equal(new[] { "One", "Two", "Three" }, ChecklistService.OpenItems(checklist).Select(i => i.Text).ToArray());
    }

    // ---------------------------------------------------------------- sections

    private Checklist SectionedChecklist()
    {
        var checklist = ChecklistService.CreateChecklist("Test", _clock, _ids);
        void Add(string text, string section) =>
            ChecklistService.AddItem(checklist, ChecklistService.CreateItem(text, section, null, null, _clock, _ids), _clock);
        Add("Fan command", "Fan");
        Add("Fan status", "Fan");
        Add("SAT sensor", "Sensors");
        Add("Loose item", "");      // blank -> General
        return checklist;
    }

    [Fact]
    public void GroupBySection_GroupsInAppearanceOrder()
    {
        var checklist = SectionedChecklist();
        var groups = ChecklistService.GroupBySection(checklist.Items);
        Assert.Equal(new[] { "Fan", "Sensors", "General" }, groups.Select(g => g.Section).ToArray());
        Assert.Equal(2, groups[0].Items.Count);
        Assert.Equal("General", groups[2].Section);
    }

    [Fact]
    public void GroupBySection_WorksForOpenAndCompletedViews()
    {
        var checklist = SectionedChecklist();
        ChecklistService.SetItemCompleted(checklist.Items[0], true, _clock); // a Fan item

        var open = ChecklistService.GroupBySection(ChecklistService.OpenItems(checklist));
        var completed = ChecklistService.GroupBySection(ChecklistService.CompletedItems(checklist));

        Assert.Contains(open, g => g.Section == "Fan" && g.Items.Count == 1);
        Assert.Contains(open, g => g.Section == "Sensors");
        Assert.Single(completed);
        Assert.Equal("Fan", completed[0].Section);
    }

    [Fact]
    public void ReorderWithinSection_KeepsSectionsStable()
    {
        var checklist = SectionedChecklist(); // Fan, Fan, Sensors, General
        var fanItems = checklist.Items.Where(i => i.Section == "Fan").ToList();
        // Move second Fan item above the first Fan item.
        ChecklistService.ReorderWithinSection(checklist.Items, fanItems[1], fanItems[0], placeAfter: false);

        var groups = ChecklistService.GroupBySection(checklist.Items);
        Assert.Equal(new[] { "Fan", "Sensors", "General" }, groups.Select(g => g.Section).ToArray());
        Assert.Equal(new[] { "Fan status", "Fan command" }, groups[0].Items.Select(i => i.Text).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, checklist.Items.Select(i => i.Order).ToArray());
    }

    // ---------------------------------------------------------------- tags

    [Theory]
    [InlineData("AHU-1; BAS; High Priority", new[] { "AHU-1", "BAS", "High Priority" })]
    [InlineData("  a ;  b  ", new[] { "a", "b" })]
    [InlineData("a;;b;", new[] { "a", "b" })]
    [InlineData("BAS; bas; Bas", new[] { "BAS" })]
    [InlineData("", new string[0])]
    public void ParseTags_TrimsDropsEmptyAndDedupes(string input, string[] expected)
        => Assert.Equal(expected, ChecklistService.ParseTags(input));

    [Fact]
    public void FormatTags_JoinsWithSemicolons()
        => Assert.Equal("AHU-1; BAS", ChecklistService.FormatTags(new[] { "AHU-1", "BAS" }));

    // ---------------------------------------------------------------- unique names

    [Theory]
    [InlineData(new string[0], "AHU", "AHU")]
    [InlineData(new[] { "AHU" }, "AHU", "AHU Copy")]
    [InlineData(new[] { "AHU", "AHU Copy" }, "AHU", "AHU Copy 2")]
    public void MakeUniqueName_FollowsCopyConvention(string[] existing, string desired, string expected)
        => Assert.Equal(expected, ChecklistService.MakeUniqueName(existing, desired));
}

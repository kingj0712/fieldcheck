using System.Text.Json;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using FieldCheck.Core.Utilities;
using Xunit;

namespace FieldCheck.Tests;

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly FileStorageService _storage;

    public FileStorageServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "FieldCheckTests", Guid.NewGuid().ToString("N"));
        _storage = new FileStorageService(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private static AppState SampleState()
    {
        var state = new AppState
        {
            Settings = new AppSettings
            {
                Theme = ThemeMode.Dark,
                LastOpenedChecklistId = "checklist-001",
                LastSelectedTab = "all",
                WindowWidth = 1024,
                WindowHeight = 768,
                SidebarWidth = 280,
                SidebarCollapsed = true
            }
        };
        var project = new Project
        {
            Id = "project-001", Name = "Commissioning", Order = 1,
            CreatedAt = new DateTime(2026, 6, 2, 8, 0, 0), UpdatedAt = new DateTime(2026, 6, 2, 8, 15, 0),
            Checklists =
            {
                new Checklist
                {
                    Id = "checklist-001", Name = "AHU_1", Order = 1,
                    CreatedAt = new DateTime(2026, 6, 2, 8, 0, 0), UpdatedAt = new DateTime(2026, 6, 2, 8, 15, 0),
                    Items =
                    {
                        new ChecklistItem
                        {
                            Id = "item-001", Text = "Verify supply fan command", Section = "Fan",
                            Notes = "Command fan.", Tags = { "AHU-1", "BAS" }, Order = 1,
                            Status = ItemStatus.Complete, Completed = true,
                            CompletedAt = new DateTime(2026, 6, 2, 8, 10, 0),
                            CreatedAt = new DateTime(2026, 6, 2, 8, 0, 0), UpdatedAt = new DateTime(2026, 6, 2, 8, 10, 0)
                        }
                    }
                }
            }
        };
        state.Projects.Add(project);
        return state;
    }

    [Fact]
    public void Save_CreatesDataFile()
    {
        _storage.Save(SampleState());
        Assert.True(File.Exists(_storage.DataFilePath));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsState()
    {
        _storage.Save(SampleState());
        var result = _storage.Load();

        Assert.Equal(LoadStatus.Loaded, result.Status);
        var project = Assert.Single(result.State.Projects);
        Assert.Equal("Commissioning", project.Name);
        var checklist = Assert.Single(project.Checklists);
        Assert.Equal("AHU_1", checklist.Name);
        var item = Assert.Single(checklist.Items);
        Assert.Equal("Verify supply fan command", item.Text);
        Assert.Equal(new[] { "AHU-1", "BAS" }, item.Tags);
        Assert.True(item.Completed);
        Assert.Equal(new DateTime(2026, 6, 2, 8, 10, 0), item.CompletedAt);
    }

    [Fact]
    public void Load_RestoresThemeAndLastSelection()
    {
        _storage.Save(SampleState());
        var settings = _storage.Load().State.Settings;
        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.Equal("checklist-001", settings.LastOpenedChecklistId);
        Assert.Equal("all", settings.LastSelectedTab);
        Assert.Equal(280, settings.SidebarWidth);
        Assert.True(settings.SidebarCollapsed);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsStatusAndIssueNote()
    {
        var state = SampleState();
        state.Projects[0].Checklists[0].Items.Add(new ChecklistItem
        {
            Id = "item-002", Text = "Bad sensor", Section = "Sensors", Order = 2,
            Status = ItemStatus.Issue, IssueNote = "Reads high",
            CreatedAt = new DateTime(2026, 6, 2, 8, 0, 0), UpdatedAt = new DateTime(2026, 6, 2, 8, 0, 0)
        });
        _storage.Save(state);

        var items = _storage.Load().State.Projects.Single().Checklists.Single().Items;
        var issue = items.Single(i => i.Id == "item-002");
        Assert.Equal(ItemStatus.Issue, issue.Status);
        Assert.Equal("Reads high", issue.IssueNote);
        Assert.False(issue.Completed);

        var done = items.Single(i => i.Id == "item-001");
        Assert.Equal(ItemStatus.Complete, done.Status);
        Assert.True(done.Completed);
    }

    [Fact]
    public void Load_MigratesCompletedBooleanToStatus()
    {
        Directory.CreateDirectory(_dir);
        var json = "{ \"version\": 2, \"settings\": {}, \"projects\": [ { \"id\": \"p\", \"name\": \"P\", \"order\": 1, \"checklists\": [ " +
                   "{ \"id\": \"c\", \"name\": \"C\", \"order\": 1, \"items\": [ " +
                   "{ \"id\": \"i1\", \"text\": \"done\", \"section\": \"S\", \"order\": 1, \"completed\": true, \"completedAt\": \"2026-06-02T08:10:00\", \"createdAt\": \"2026-06-02T08:00:00\", \"updatedAt\": \"2026-06-02T08:00:00\" }, " +
                   "{ \"id\": \"i2\", \"text\": \"open\", \"section\": \"S\", \"order\": 2, \"completed\": false, \"createdAt\": \"2026-06-02T08:00:00\", \"updatedAt\": \"2026-06-02T08:00:00\" } " +
                   "] } ] } ] }";
        File.WriteAllText(_storage.DataFilePath, json);

        var items = _storage.Load().State.Projects.Single().Checklists.Single().Items;
        Assert.Equal(ItemStatus.Complete, items[0].Status);
        Assert.Equal(new DateTime(2026, 6, 2, 8, 10, 0), items[0].CompletedAt); // completion timestamp preserved
        Assert.Equal(ItemStatus.Open, items[1].Status);
    }

    [Fact]
    public void Load_MissingFile_StartsEmpty()
    {
        var result = _storage.Load();
        Assert.Equal(LoadStatus.StartedEmptyMissing, result.Status);
        Assert.Empty(result.State.Projects);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Load_CorruptPrimaryWithValidBackup_RestoresFromBackup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_storage.BackupFilePath, JsonSerializer.Serialize(SampleState(), AppJson.Options));
        File.WriteAllText(_storage.DataFilePath, "{ this is not valid json ");

        var result = _storage.Load();
        Assert.Equal(LoadStatus.RestoredFromBackup, result.Status);
        Assert.NotNull(result.Message);
        Assert.Single(result.State.Projects);
    }

    [Fact]
    public void Load_CorruptPrimaryAndBackup_StartsEmptyWithoutCrashing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_storage.DataFilePath, "not json");
        File.WriteAllText(_storage.BackupFilePath, "also not json");

        var result = _storage.Load();
        Assert.Equal(LoadStatus.StartedEmptyCorrupt, result.Status);
        Assert.Empty(result.State.Projects);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void Load_InvalidJson_DoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_storage.DataFilePath, "}{ totally broken ][");
        Assert.Null(Record.Exception(() => _storage.Load()));
    }

    [Fact]
    public void Save_KeepsPreviousVersionAsBackup()
    {
        var first = SampleState();
        first.Projects[0].Name = "First Version";
        _storage.Save(first);

        var second = SampleState();
        second.Projects[0].Name = "Second Version";
        _storage.Save(second);

        var live = JsonSerializer.Deserialize<AppState>(File.ReadAllText(_storage.DataFilePath), AppJson.Options)!;
        var backup = JsonSerializer.Deserialize<AppState>(File.ReadAllText(_storage.BackupFilePath), AppJson.Options)!;
        Assert.Equal("Second Version", live.Projects[0].Name);
        Assert.Equal("First Version", backup.Projects[0].Name);
    }

    [Fact]
    public void Save_DoesNotLeaveTempFileBehind()
    {
        _storage.Save(SampleState());
        Assert.False(File.Exists(_storage.DataFilePath + ".tmp"));
    }

    [Fact]
    public void Load_MigratesV1RootChecklistsIntoGeneralProject()
    {
        Directory.CreateDirectory(_dir);
        var v1 = "{\n" +
                 "  \"version\": 1,\n" +
                 "  \"settings\": { \"theme\": \"dark\", \"lastOpenedChecklistId\": \"checklist-001\", \"lastSelectedTab\": \"open\", \"windowWidth\": 1200, \"windowHeight\": 800 },\n" +
                 "  \"checklists\": [\n" +
                 "    { \"id\": \"checklist-001\", \"name\": \"AHU Checkout\", \"projectName\": \"Old Label\", \"order\": 1, \"createdAt\": \"2026-06-02T08:00:00\", \"updatedAt\": \"2026-06-02T08:15:00\", \"items\": [\n" +
                 "      { \"id\": \"item-001\", \"text\": \"Verify fan\", \"section\": \"Fan\", \"notes\": \"n\", \"order\": 1, \"completed\": true, \"completedAt\": \"2026-06-02T08:10:00\", \"createdAt\": \"2026-06-02T08:00:00\", \"updatedAt\": \"2026-06-02T08:10:00\" }\n" +
                 "    ] }\n" +
                 "  ]\n" +
                 "}";
        File.WriteAllText(_storage.DataFilePath, v1);

        var result = _storage.Load();

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(AppState.CurrentVersion, result.State.Version);
        var project = Assert.Single(result.State.Projects);
        Assert.Equal("General", project.Name);
        var checklist = Assert.Single(project.Checklists);
        Assert.Equal("AHU Checkout", checklist.Name);
        var item = Assert.Single(checklist.Items);
        Assert.Equal("Verify fan", item.Text);
        Assert.Equal("Fan", item.Section);
        Assert.True(item.Completed);
        Assert.Equal(ItemStatus.Complete, item.Status); // completed boolean migrated to a status
        Assert.Equal(new DateTime(2026, 6, 2, 8, 10, 0), item.CompletedAt);
        Assert.Equal(1, item.Order);
        Assert.Empty(item.Tags); // initialized empty by migration
        Assert.Equal("checklist-001", result.State.Settings.LastOpenedChecklistId); // settings preserved
        Assert.Equal(300, result.State.Settings.SidebarWidth);  // new fields default cleanly for old files
        Assert.False(result.State.Settings.SidebarCollapsed);
    }
}

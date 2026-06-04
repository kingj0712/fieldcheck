using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using FieldCheck.Core.Models;
using FieldCheck.Core.Utilities;

namespace FieldCheck.Core.Services;

/// <summary>The outcome of a load attempt, used by the UI to decide whether to warn the user.</summary>
public enum LoadStatus
{
    /// <summary>data.json loaded cleanly.</summary>
    Loaded,
    /// <summary>data.json was missing or unreadable, so the backup was restored.</summary>
    RestoredFromBackup,
    /// <summary>No data file existed yet; this is a normal first run.</summary>
    StartedEmptyMissing,
    /// <summary>Both files were unreadable; started empty without touching the old files.</summary>
    StartedEmptyCorrupt
}

public sealed class LoadResult
{
    public required AppState State { get; init; }
    public required LoadStatus Status { get; init; }
    public string? Message { get; init; }
}

/// <summary>Thrown when a save cannot be completed; the app surfaces this as "Unable to save".</summary>
public sealed class FileStorageException : Exception
{
    public FileStorageException(string message) : base(message) { }
    public FileStorageException(string message, Exception inner) : base(message, inner) { }
}

public interface IFileStorageService
{
    string DataFilePath { get; }
    string BackupFilePath { get; }
    LoadResult Load();
    void Save(AppState state);
}

/// <summary>
/// Reads and writes the app state to disk with safe, atomic semantics:
/// serialize to a temp file, validate it, then swap it into place while moving the previous
/// known-good file to the backup. Loading falls back to the backup and never throws or
/// silently discards data.
/// </summary>
public sealed class FileStorageService : IFileStorageService
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _json;

    public string DataDirectory { get; }
    public string DataFilePath { get; }
    public string BackupFilePath { get; }
    private string TempFilePath => DataFilePath + ".tmp";

    public FileStorageService(string dataDirectory, JsonSerializerOptions? json = null)
    {
        DataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        DataFilePath = Path.Combine(dataDirectory, "data.json");
        BackupFilePath = Path.Combine(dataDirectory, "data.backup.json");
        _json = json ?? AppJson.Options;
    }

    public LoadResult Load()
    {
        lock (_gate)
        {
            if (File.Exists(DataFilePath))
            {
                if (TryRead(DataFilePath, out var primary))
                    return new LoadResult { State = primary!, Status = LoadStatus.Loaded };

                // Primary is corrupt: try the backup before giving up.
                if (File.Exists(BackupFilePath) && TryRead(BackupFilePath, out var fromBackup))
                    return new LoadResult
                    {
                        State = fromBackup!,
                        Status = LoadStatus.RestoredFromBackup,
                        Message = "Your data file could not be read, so FieldCheck restored your most recent backup."
                    };

                return new LoadResult
                {
                    State = new AppState(),
                    Status = LoadStatus.StartedEmptyCorrupt,
                    Message = "FieldCheck could not read your data file or its backup, so it started with an empty workspace. " +
                              "Your existing files were left untouched."
                };
            }

            // No primary file. A lingering backup can happen if a previous save was interrupted.
            if (File.Exists(BackupFilePath) && TryRead(BackupFilePath, out var onlyBackup))
                return new LoadResult
                {
                    State = onlyBackup!,
                    Status = LoadStatus.RestoredFromBackup,
                    Message = "FieldCheck restored your workspace from the most recent backup."
                };

            // Genuine first run.
            return new LoadResult { State = new AppState(), Status = LoadStatus.StartedEmptyMissing };
        }
    }

    public void Save(AppState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                state.Version = AppState.CurrentVersion;

                var json = JsonSerializer.Serialize(state, _json);

                // 1. Write to a temp file first so a failure never corrupts the real file.
                File.WriteAllText(TempFilePath, json);

                // 2. Validate that the temp file exists and round-trips back to a real object.
                var verifyText = File.ReadAllText(TempFilePath);
                if (string.IsNullOrWhiteSpace(verifyText) ||
                    JsonSerializer.Deserialize<AppState>(verifyText, _json) is null)
                {
                    throw new FileStorageException("The temporary save file failed validation and was discarded.");
                }

                // 3. Atomically replace the live file, moving the previous good copy to the backup.
                if (File.Exists(DataFilePath))
                {
                    File.Replace(TempFilePath, DataFilePath, BackupFilePath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(TempFilePath, DataFilePath);
                }
            }
            catch (FileStorageException)
            {
                TryCleanupTemp();
                throw;
            }
            catch (Exception ex)
            {
                TryCleanupTemp();
                throw new FileStorageException("FieldCheck was unable to save your data to disk.", ex);
            }
        }
    }

    private bool TryRead(string path, out AppState? state)
    {
        state = null;
        try
        {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Parse to a mutable node first so older documents can be migrated before binding.
            var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (node is null)
                return false;

            Migrate(node);

            var parsed = node.Deserialize<AppState>(_json);
            if (parsed is null)
                return false;

            Sanitize(parsed);
            state = parsed;
            return true;
        }
        catch
        {
            // Any parse/IO failure here simply means this file is unusable; the caller decides
            // whether to fall back to the backup.
            return false;
        }
    }

    /// <summary>
    /// Upgrades an older document in place (as a JsonNode) before it is bound to <see cref="AppState"/>.
    /// v1 stored checklists at the root; I wrap those into a default "General" project so no data is lost.
    /// </summary>
    private static void Migrate(JsonNode root)
    {
        if (root is not JsonObject obj)
            return;

        // v1 -> v2: no "projects" array yet. Move any root-level "checklists" into a General project.
        if (obj["projects"] is null)
        {
            var checklists = obj["checklists"] as JsonArray;
            obj.Remove("checklists"); // detaches the array so it can be re-parented

            var now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var general = new JsonObject
            {
                ["id"] = "project-" + Guid.NewGuid().ToString("N"),
                ["name"] = "General",
                ["order"] = 1,
                ["collapsed"] = false,
                ["createdAt"] = now,
                ["updatedAt"] = now,
                ["checklists"] = checklists ?? new JsonArray()
            };
            obj["projects"] = new JsonArray(general);
        }

        obj["version"] = AppState.CurrentVersion;
    }

    /// <summary>Defensively repairs a loaded document so the rest of the app can assume non-null collections.</summary>
    private static void Sanitize(AppState state)
    {
        state.Settings ??= new AppSettings();
        state.Projects ??= new List<Project>();
        if (string.IsNullOrWhiteSpace(state.Settings.LastSelectedTab))
            state.Settings.LastSelectedTab = "open";

        foreach (var project in state.Projects)
        {
            project.Name ??= string.Empty;
            project.Checklists ??= new List<Checklist>();
            foreach (var checklist in project.Checklists)
            {
                checklist.Name ??= string.Empty;
                checklist.Items ??= new List<ChecklistItem>();
                foreach (var item in checklist.Items)
                {
                    item.Text ??= string.Empty;
                    item.Section = string.IsNullOrWhiteSpace(item.Section) ? "General" : item.Section;
                    item.Notes ??= string.Empty;
                    item.Tags ??= new List<string>();
                    item.IssueNote ??= string.Empty;

                    // v2 -> v3: files written before statuses only had the boolean "completed".
                    // A missing "status" binds to the enum default (Open), so an item that was
                    // completed needs promoting to Complete. Then keep the legacy mirror and the
                    // completion timestamp consistent with the canonical Status.
                    if (item.Status == ItemStatus.Open && item.Completed)
                        item.Status = ItemStatus.Complete;
                    item.Completed = item.Status == ItemStatus.Complete;
                    if (item.Status != ItemStatus.Complete)
                        item.CompletedAt = null;
                }
            }
        }

        state.Version = AppState.CurrentVersion;
    }

    private void TryCleanupTemp()
    {
        try
        {
            if (File.Exists(TempFilePath))
                File.Delete(TempFilePath);
        }
        catch
        {
            // Best-effort cleanup; ignore failures.
        }
    }
}

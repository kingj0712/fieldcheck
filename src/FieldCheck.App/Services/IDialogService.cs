using FieldCheck.Core.Services;

namespace FieldCheck.App.Services;

/// <summary>Add/edit-item input. Tags are the raw semicolon-separated editor text.</summary>
public sealed record ItemEditInput(string Text, string Section, string Notes, string Tags);

/// <summary>
/// Abstracts the modal dialogs and file pickers so the view models stay free of direct WPF
/// window references. Implemented by <see cref="DialogService"/>.
/// </summary>
public interface IDialogService
{
    /// <summary>Single-line text prompt (new project/checklist/section, rename). Returns null if cancelled or blank.</summary>
    string? PromptText(string title, string label, string initialValue, string confirmText = "Save");

    /// <summary>Add/edit item prompt (text, section + reuse chips, tags, notes). Returns null if cancelled.</summary>
    ItemEditInput? PromptItem(string title, ItemEditInput initial, IEnumerable<string> knownSections);

    /// <summary>Yes/No confirmation. Returns true when the user confirms.</summary>
    bool Confirm(string title, string message, string confirmText = "Delete", bool destructive = true);

    void ShowMessage(string title, string message);

    void ShowImportSummary(CsvImportResult result, string sourceName);

    /// <summary>Open-file picker for a CSV. Returns the chosen path or null.</summary>
    string? OpenCsvFile();

    /// <summary>Save-file picker. Returns the chosen path or null.</summary>
    string? SaveFile(string title, string suggestedFileName,
        string filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*");
}

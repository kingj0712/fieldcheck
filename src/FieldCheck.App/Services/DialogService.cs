using System.Text;
using System.Windows;
using FieldCheck.App.Views;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;
using Microsoft.Win32;

namespace FieldCheck.App.Services;

/// <summary>WPF implementation of the dialogs and file pickers, owned by the main window.</summary>
public sealed class DialogService : IDialogService
{
    public Window? OwnerWindow { get; set; }

    public string? PromptText(string title, string label, string initialValue, string confirmText = "Save")
    {
        var dialog = CreateEntry();
        dialog.Configure(title, label, initialValue, confirmText: confirmText);
        return dialog.ShowDialog() == true ? dialog.Field1Text : null;
    }

    public ItemEditInput? PromptItem(string title, ItemEditInput initial, IEnumerable<string> knownSections)
    {
        var dialog = CreateEntry();
        dialog.Configure(
            title,
            field1Label: "Item text", field1Value: initial.Text,
            showField2: true, field2Label: "Section (optional, defaults to General)", field2Value: initial.Section,
            showField3: true, field3Label: "Tags (optional, separate with ;)", field3Value: initial.Tags,
            showNotes: true, notesValue: initial.Notes,
            chips: knownSections,
            confirmText: "Save");
        return dialog.ShowDialog() == true
            ? new ItemEditInput(dialog.Field1Text, dialog.Field2Text, dialog.NotesText, dialog.Field3Text)
            : null;
    }

    public bool Confirm(string title, string message, string confirmText = "Delete", bool destructive = true)
    {
        var dialog = new MessageDialog { Owner = OwnerWindow };
        dialog.AsConfirm(title, message, confirmText, destructive);
        return dialog.ShowDialog() == true;
    }

    public void ShowMessage(string title, string message)
    {
        var dialog = new MessageDialog { Owner = OwnerWindow };
        dialog.AsMessage(title, message);
        dialog.ShowDialog();
    }

    public void ShowImportSummary(CsvImportResult result, string sourceName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Imported from {sourceName}.");
        sb.AppendLine();
        sb.AppendLine($"•  Projects created: {result.ProjectsCreated}");
        if (result.ProjectsReused > 0)
            sb.AppendLine($"•  Projects reused: {result.ProjectsReused}");
        sb.AppendLine($"•  Checklists created: {result.ChecklistsCreated}");
        sb.AppendLine($"•  Items imported: {result.ItemsImported}");
        sb.AppendLine($"•  Rows skipped: {result.RowsSkipped}");

        if (result.Issues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Notes:");
            foreach (var issue in result.Issues)
                sb.AppendLine($"•  {issue}");
        }

        ShowMessage("Import complete", sb.ToString().TrimEnd());
    }

    public SearchResult? ShowGlobalSearch(AppState state)
    {
        var dialog = new GlobalSearchDialog(state) { Owner = OwnerWindow };
        return dialog.ShowDialog() == true ? dialog.SelectedResult : null;
    }

    public string? OpenCsvFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    public string? SaveFile(string title, string suggestedFileName,
        string filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedFileName,
            Filter = filter,
            OverwritePrompt = true,
            AddExtension = true,
            DefaultExt = "csv"
        };
        return Show(dialog) ? dialog.FileName : null;
    }

    private EntryDialog CreateEntry() => new() { Owner = OwnerWindow };

    private bool Show(FileDialog dialog) =>
        (OwnerWindow is not null ? dialog.ShowDialog(OwnerWindow) : dialog.ShowDialog()) == true;
}

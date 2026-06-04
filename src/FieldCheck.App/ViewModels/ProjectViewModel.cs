using System.Collections.ObjectModel;
using System.Windows.Input;
using FieldCheck.App.Behaviors;
using FieldCheck.App.Services;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.ViewModels;

/// <summary>Wraps a <see cref="Project"/>: a collapsible sidebar group of checklists.</summary>
public sealed class ProjectViewModel : BindableBase
{
    private readonly AppServices _services;
    private readonly IWorkspaceHost _host;
    private bool _suppressSelection;

    public ProjectViewModel(Project model, AppServices services, IWorkspaceHost host)
    {
        Model = model;
        _services = services;
        _host = host;

        Checklists = new ObservableCollection<ChecklistViewModel>();
        foreach (var checklist in model.Checklists.OrderBy(c => c.Order))
            Checklists.Add(new ChecklistViewModel(checklist, this, services, host));

        // Keep the overview's checklist list and aggregate counts current as checklists come and go.
        Checklists.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasChecklists));
            RefreshProgress();
        };

        AddChecklistCommand = new RelayCommand(() => _host.RequestNewChecklist(this));
        DeleteCommand = new RelayCommand(() => _host.RequestDeleteProject(this));
        RenameCommand = new RelayCommand(Rename);
        ExportReportCommand = new RelayCommand(ExportReport);
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        ReorderChecklistsCommand = new RelayCommand<ReorderRequest>(ReorderChecklists);
    }

    public Project Model { get; }
    public string Id => Model.Id;

    public ObservableCollection<ChecklistViewModel> Checklists { get; }

    public ICommand AddChecklistCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand ReorderChecklistsCommand { get; }

    /// <summary>True when this project's overview is the active selection in the main pane.</summary>
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Opens a themed prompt to rename the project (overview button + context menu).</summary>
    private void Rename()
    {
        var name = _services.Dialogs.PromptText("Rename project", "Project name", Model.Name, "Rename");
        if (!string.IsNullOrWhiteSpace(name))
            Name = name; // routed through the Name setter: trims, timestamps, refreshes breadcrumbs, autosaves
    }

    private void ExportReport()
    {
        var path = _services.Dialogs.SaveFile("Export project report as Markdown",
            WorkspaceExportService.SanitizeFileName(Model.Name) + " - project report.md",
            "Markdown file (*.md)|*.md|All files (*.*)|*.*", defaultExt: "md");
        if (path is null)
            return;
        try
        {
            System.IO.File.WriteAllText(path, MarkdownReportService.ProjectReport(Model, _services.Clock.Now));
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Export failed", $"FieldCheck could not export the project report:\n\n{ex.Message}");
        }
    }

    public string Name
    {
        get => Model.Name;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                OnPropertyChanged(nameof(Name));
                return;
            }
            if (string.Equals(trimmed, Model.Name, StringComparison.Ordinal))
                return;
            ProjectService.RenameProject(Model, trimmed, _services.Clock);
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdatedAtText));
            _host.NotifyProjectRenamed(this);
            _services.Save(immediate: true);
        }
    }

    /// <summary>Aggregate progress across this project's checklists, e.g. "12/40" (blank if no items).</summary>
    public string ProgressText
    {
        get
        {
            var total = Model.Checklists.Sum(c => c.Items.Count);
            if (total == 0)
                return string.Empty;
            var done = Model.Checklists.Sum(c => c.Items.Count(i => i.Status == ItemStatus.Complete));
            return $"{done}/{total}";
        }
    }

    public bool HasProgress => Model.Checklists.Any(c => c.Items.Count > 0);

    // ---------------------------------------------------------------- project overview aggregates

    public bool HasChecklists => Checklists.Count > 0;
    public int ChecklistCount => Model.Checklists.Count;
    public int TotalItems => ProjectService.GetProgress(Model).TotalItems;
    public int OpenItems => ProjectService.GetProgress(Model).OpenItems;
    public int IssueItems => ProjectService.GetProgress(Model).IssueItems;
    public int CompletedItems => ProjectService.GetProgress(Model).CompletedItems;
    public int NaItems => ProjectService.GetProgress(Model).NotApplicableItems;

    /// <summary>ProgressBar maximum (never zero so an empty project shows an empty—not full—bar).</summary>
    public int ProgressMaximum => TotalItems == 0 ? 1 : TotalItems;

    /// <summary>e.g. "12 of 42 items complete", or a friendly note when the project has no items yet.</summary>
    public string ProgressSummary
    {
        get
        {
            var p = ProjectService.GetProgress(Model);
            return p.TotalItems == 0 ? "No items yet" : $"{p.CompletedItems} of {p.TotalItems} items complete";
        }
    }

    public string UpdatedAtText => $"Updated {Model.UpdatedAt:MMM d, h:mm tt}";

    /// <summary>Called by child checklists when their items change so the sidebar badge and the
    /// project overview aggregates stay current.</summary>
    public void RefreshProgress()
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(HasProgress));
        OnPropertyChanged(nameof(ChecklistCount));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(OpenItems));
        OnPropertyChanged(nameof(IssueItems));
        OnPropertyChanged(nameof(CompletedItems));
        OnPropertyChanged(nameof(NaItems));
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(ProgressSummary));
        OnPropertyChanged(nameof(UpdatedAtText));
    }

    public bool IsExpanded
    {
        get => !Model.Collapsed;
        set
        {
            if (Model.Collapsed == !value)
                return;
            Model.Collapsed = !value;
            OnPropertyChanged();
            _services.Save();
        }
    }

    private ChecklistViewModel? _selectedChecklistInProject;
    public ChecklistViewModel? SelectedChecklistInProject
    {
        get => _selectedChecklistInProject;
        set
        {
            if (!SetProperty(ref _selectedChecklistInProject, value))
                return;
            // Only a real user pick promotes to the global selection; programmatic clears do nothing.
            if (!_suppressSelection && value is not null)
                _host.OnChecklistSelected(value);
        }
    }

    /// <summary>Reflects the single global selection into this project's list without looping back to the host.</summary>
    public void SyncSelection(ChecklistViewModel? globalSelected)
    {
        _suppressSelection = true;
        SelectedChecklistInProject = globalSelected is not null && Checklists.Contains(globalSelected)
            ? globalSelected
            : null;
        _suppressSelection = false;
    }

    private void ReorderChecklists(ReorderRequest? request)
    {
        if (request is null || request.Source is not ChecklistViewModel source || !Checklists.Contains(source))
            return;

        Checklists.Remove(source);
        if (request.Target is ChecklistViewModel target && Checklists.Contains(target))
        {
            var index = Checklists.IndexOf(target);
            if (request.PlaceBelow)
                index++;
            Checklists.Insert(Math.Clamp(index, 0, Checklists.Count), source);
        }
        else
        {
            Checklists.Add(source);
        }

        Model.Checklists.Clear();
        foreach (var checklist in Checklists)
            Model.Checklists.Add(checklist.Model);
        ProjectService.NormalizeChecklistOrders(Model.Checklists);
        Model.UpdatedAt = _services.Clock.Now;
        _services.Save(immediate: true);
    }
}

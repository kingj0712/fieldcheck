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
        ToggleExpandCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        ReorderChecklistsCommand = new RelayCommand<ReorderRequest>(ReorderChecklists);
    }

    public Project Model { get; }
    public string Id => Model.Id;

    public ObservableCollection<ChecklistViewModel> Checklists { get; }

    public ICommand AddChecklistCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand ReorderChecklistsCommand { get; }

    public string Name
    {
        get => Model.Name;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                OnPropertyChanged(nameof(Name)); // revert the inline editor to the existing name
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

    /// <summary>Opens a themed prompt to rename the project (overview button and context menu).</summary>
    private void Rename()
    {
        // The prompt returns null when cancelled or blank; I set the name through the same path as
        // the inline editor so trimming, timestamp, breadcrumb refresh, and autosave all run once.
        var name = _services.Dialogs.PromptText("Rename project", "Project name", Model.Name, "Rename");
        if (!string.IsNullOrWhiteSpace(name))
            Name = name;
    }

    /// <summary>True when the project's overview is the active selection in the main pane.</summary>
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Aggregate progress across this project's checklists, e.g. "12/40" (blank if no items).</summary>
    public string ProgressText
    {
        get
        {
            var total = Model.Checklists.Sum(c => c.Items.Count);
            if (total == 0)
                return string.Empty;
            var done = Model.Checklists.Sum(c => c.Items.Count(i => i.Completed));
            return $"{done}/{total}";
        }
    }

    public bool HasProgress => Model.Checklists.Any(c => c.Items.Count > 0);

    // ---------------------------------------------------------------- project overview aggregates

    public bool HasChecklists => Checklists.Count > 0;
    public int ChecklistCount => Model.Checklists.Count;
    public int TotalItems => ProjectService.GetProgress(Model).TotalItems;
    public int CompletedItems => ProjectService.GetProgress(Model).CompletedItems;
    public int OpenItems => ProjectService.GetProgress(Model).OpenItems;

    /// <summary>ProgressBar maximum (never zero so an empty project shows an empty—not full—bar).</summary>
    public int ProgressMaximum => TotalItems == 0 ? 1 : TotalItems;

    /// <summary>e.g. "12 of 42 items complete", or a friendly note when the project has no items yet.</summary>
    public string ProgressSummary
    {
        get
        {
            var p = ProjectService.GetProgress(Model);
            return p.TotalItems == 0
                ? "No items yet"
                : $"{p.CompletedItems} of {p.TotalItems} items complete";
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
        OnPropertyChanged(nameof(CompletedItems));
        OnPropertyChanged(nameof(OpenItems));
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

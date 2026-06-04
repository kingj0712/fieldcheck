using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using FieldCheck.App.Services;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.ViewModels;

/// <summary>Top-level view model: the project-grouped sidebar, the selected checklist, theme, and autosave status.</summary>
public sealed class MainViewModel : BindableBase, IWorkspaceHost
{
    private readonly AppServices _services;
    private AppState State => _services.State.State;

    public MainViewModel(AppServices services, string? startupNotice)
    {
        _services = services;
        StartupNotice = startupNotice;

        Projects = new ObservableCollection<ProjectViewModel>();
        foreach (var project in State.Projects.OrderBy(p => p.Order))
            Projects.Add(new ProjectViewModel(project, services, this));

        NewProjectCommand = new RelayCommand(NewProject);
        NewChecklistCommand = new RelayCommand(() => CreateChecklistInProject(null));
        ImportCsvCommand = new RelayCommand(ImportCsv);
        DownloadTemplateCommand = new RelayCommand(DownloadTemplate);
        ExportWorkspaceCommand = new RelayCommand(ExportWorkspace, () => HasProjects);
        OpenGlobalSearchCommand = new RelayCommand(OpenGlobalSearch);
        SetLightThemeCommand = new RelayCommand(() => SetTheme(ThemeMode.Light));
        SetDarkThemeCommand = new RelayCommand(() => SetTheme(ThemeMode.Dark));
        SetSystemThemeCommand = new RelayCommand(() => SetTheme(ThemeMode.System));

        CurrentThemeMode = _services.Theme.CurrentMode;
        _services.State.PropertyChanged += OnStateServiceChanged;

        RestoreSelection();
    }

    /// <summary>Restores the last main-pane selection: a project overview if one was last shown,
    /// otherwise the last checklist (falling back to the first checklist).</summary>
    private void RestoreSelection()
    {
        var projectToOpen = State.Settings.LastOpenedProjectId is { } projectId
            ? Projects.FirstOrDefault(p => p.Id == projectId)
            : null;
        if (projectToOpen is not null)
        {
            SelectProject(projectToOpen);
            return;
        }

        SelectedChecklist =
            Projects.SelectMany(p => p.Checklists).FirstOrDefault(c => c.Id == State.Settings.LastOpenedChecklistId)
            ?? Projects.SelectMany(p => p.Checklists).FirstOrDefault();
    }

    public ObservableCollection<ProjectViewModel> Projects { get; }

    public ICommand NewProjectCommand { get; }
    public ICommand NewChecklistCommand { get; }
    public ICommand ImportCsvCommand { get; }
    public ICommand DownloadTemplateCommand { get; }
    public RelayCommand ExportWorkspaceCommand { get; }
    public ICommand OpenGlobalSearchCommand { get; }
    public ICommand SetLightThemeCommand { get; }
    public ICommand SetDarkThemeCommand { get; }
    public ICommand SetSystemThemeCommand { get; }

    public bool HasProjects => Projects.Count > 0;
    public bool ShowWelcome => Projects.Count == 0;

    public string? StartupNotice { get; }

    private ChecklistViewModel? _selectedChecklist;
    public ChecklistViewModel? SelectedChecklist
    {
        get => _selectedChecklist;
        set
        {
            // Re-selecting the same checklist is a no-op only when no project overview is showing;
            // otherwise we still need to switch the main pane away from the overview.
            if (ReferenceEquals(_selectedChecklist, value) && _selectedProject is null)
                return;
            if (_selectedChecklist is not null)
                _selectedChecklist.IsSelected = false;

            _selectedChecklist = value;

            if (value is not null)
            {
                value.IsSelected = true;
                value.InitViewMode(State.Settings.LastSelectedTab);
                ClearProjectSelection();              // opening a checklist leaves the project overview
                State.Settings.LastOpenedProjectId = null;
            }
            State.Settings.LastOpenedChecklistId = value?.Id;

            foreach (var project in Projects)
                project.SyncSelection(value);

            OnPropertyChanged();
            RaiseSelectionFlags();
            _services.Save();
        }
    }

    private ProjectViewModel? _selectedProject;
    /// <summary>The project whose overview is shown in the main pane, or null. Set via <see cref="SelectProject"/>.</summary>
    public ProjectViewModel? SelectedProject => _selectedProject;

    /// <summary>Selects a project and shows its overview in the main pane, clearing any checklist
    /// selection. Read-only with respect to data: never reorders or changes completion.</summary>
    public void SelectProject(ProjectViewModel project)
    {
        if (project is null)
            return;

        if (_selectedChecklist is not null)
        {
            _selectedChecklist.IsSelected = false;
            _selectedChecklist = null;
            OnPropertyChanged(nameof(SelectedChecklist));
        }

        foreach (var p in Projects)
        {
            p.IsSelected = ReferenceEquals(p, project);
            p.SyncSelection(null);                    // clear the sidebar checklist highlight
        }

        _selectedProject = project;
        project.IsExpanded = true;
        State.Settings.LastOpenedChecklistId = null;
        State.Settings.LastOpenedProjectId = project.Id;

        OnPropertyChanged(nameof(SelectedProject));
        RaiseSelectionFlags();
        _services.Save();
    }

    private void ClearProjectSelection()
    {
        if (_selectedProject is null)
            return;
        _selectedProject.IsSelected = false;
        _selectedProject = null;
        OnPropertyChanged(nameof(SelectedProject));
    }

    private void RaiseSelectionFlags()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowChecklistView));
        OnPropertyChanged(nameof(ShowProjectOverview));
        OnPropertyChanged(nameof(ShowNothingSelected));
    }

    public bool HasSelection => SelectedChecklist is not null || SelectedProject is not null;
    public bool ShowChecklistView => SelectedChecklist is not null;
    public bool ShowProjectOverview => SelectedProject is not null && SelectedChecklist is null;
    public bool ShowNothingSelected => SelectedChecklist is null && SelectedProject is null;

    // ---------------------------------------------------------------- theme

    public ThemeMode CurrentThemeMode { get; private set; }
    public bool IsLightTheme => CurrentThemeMode == ThemeMode.Light;
    public bool IsDarkTheme => CurrentThemeMode == ThemeMode.Dark;
    public bool IsSystemTheme => CurrentThemeMode == ThemeMode.System;

    private void SetTheme(ThemeMode mode)
    {
        _services.Theme.Apply(mode);
        State.Settings.Theme = mode;
        CurrentThemeMode = mode;
        OnPropertyChanged(nameof(CurrentThemeMode));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsSystemTheme));
        _services.Save();
    }

    // ---------------------------------------------------------------- create / import

    private void NewProject()
    {
        var name = _services.Dialogs.PromptText("New project", "Project name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return;
        var project = ProjectService.CreateProject(name, _services.Clock, _services.Ids);
        ProjectService.AddProject(State, project);
        var vm = new ProjectViewModel(project, _services, this);
        Projects.Add(vm);
        RefreshWorkspaceState();
        _services.Save(immediate: true);
    }

    public void RequestNewChecklist(ProjectViewModel project) => CreateChecklistInProject(project);

    private void CreateChecklistInProject(ProjectViewModel? project)
    {
        project ??= GetTargetProject();

        var name = _services.Dialogs.PromptText("New checklist", "Checklist name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var checklist = ChecklistService.CreateChecklist(name, _services.Clock, _services.Ids);
        ProjectService.AddChecklist(project.Model, checklist, _services.Clock);
        var vm = new ChecklistViewModel(checklist, project, _services, this);
        project.Checklists.Add(vm);
        project.IsExpanded = true;
        SelectedChecklist = vm;
        RefreshWorkspaceState();
        _services.Save(immediate: true);
    }

    private ProjectViewModel GetTargetProject()
    {
        if (SelectedProject is not null)
            return SelectedProject;
        if (SelectedChecklist is not null)
            return SelectedChecklist.Project;
        if (Projects.Count > 0)
            return Projects[0];

        // No projects yet: create the default one.
        var general = ProjectService.CreateProject(ProjectService.DefaultProjectName, _services.Clock, _services.Ids);
        ProjectService.AddProject(State, general);
        var vm = new ProjectViewModel(general, _services, this);
        Projects.Add(vm);
        RefreshWorkspaceState();
        return vm;
    }

    private void ImportCsv()
    {
        var path = _services.Dialogs.OpenCsvFile();
        if (path is null)
            return;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Import failed", $"FieldCheck could not read the file:\n\n{ex.Message}");
            return;
        }

        CsvImportResult result;
        try
        {
            var fallback = Path.GetFileNameWithoutExtension(path);
            result = CsvImportService.Import(text, fallback, State, _services.Clock, _services.Ids);
        }
        catch (CsvImportException ex)
        {
            _services.Dialogs.ShowMessage("Import problem", ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Import failed", $"FieldCheck could not import the CSV:\n\n{ex.Message}");
            return;
        }

        RebuildProjects(result.CreatedChecklistIds.FirstOrDefault());
        _services.Save(immediate: true);
        _services.Dialogs.ShowImportSummary(result, Path.GetFileName(path));
    }

    private void DownloadTemplate()
    {
        var path = _services.Dialogs.SaveFile("Save CSV template", "FieldCheck-template.csv");
        if (path is null)
            return;
        try
        {
            File.WriteAllText(path, TemplateService.GetTemplateCsv());
            _services.Dialogs.ShowMessage("Template saved", $"A CSV template was saved to:\n\n{path}");
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Couldn't save template", ex.Message);
        }
    }

    /// <summary>Exports the whole workspace (raw data.json + per-checklist CSV/Markdown + a template
    /// and README) into a timestamped folder the user chooses.</summary>
    private void ExportWorkspace()
    {
        var parent = _services.Dialogs.PickFolder("Choose where to export the workspace");
        if (parent is null)
            return;

        _services.State.Flush(); // ensure the on-disk data.json reflects the latest edits before we copy it

        var folderName = WorkspaceExportService.SuggestedFolderName(_services.Clock.Now);
        var target = Path.Combine(parent, folderName);
        if (Directory.Exists(target) &&
            !_services.Dialogs.Confirm("Export already exists",
                $"“{folderName}” already exists here. Overwrite its contents?", "Overwrite", destructive: false))
            return;

        try
        {
            string? rawJson = null;
            try
            {
                if (File.Exists(_services.State.DataFilePath))
                    rawJson = File.ReadAllText(_services.State.DataFilePath);
            }
            catch { /* the service will serialize current state instead */ }

            WorkspaceExportService.Export(State, rawJson, target, _services.Clock.Now);
            _services.Dialogs.ShowMessage("Workspace exported", $"FieldCheck exported your workspace to:\n\n{target}");
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Export failed", $"FieldCheck could not export the workspace:\n\n{ex.Message}");
        }
    }

    /// <summary>Rebuilds the sidebar view models from state (used after import) and restores a selection.</summary>
    private void RebuildProjects(string? selectChecklistId)
    {
        Projects.Clear();
        foreach (var project in State.Projects.OrderBy(p => p.Order))
            Projects.Add(new ProjectViewModel(project, _services, this));
        RefreshWorkspaceState();

        var target = Projects.SelectMany(p => p.Checklists).FirstOrDefault(c => c.Id == selectChecklistId)
                     ?? Projects.SelectMany(p => p.Checklists).FirstOrDefault();
        // The old view models are gone; reset both selections so the setter re-applies cleanly.
        _selectedProject = null;
        _selectedChecklist = null;
        SelectedChecklist = target;
        RaiseSelectionFlags();
    }

    // ---------------------------------------------------------------- global search

    private void OpenGlobalSearch()
    {
        var result = _services.Dialogs.ShowGlobalSearch(State);
        if (result is not null)
            NavigateTo(result);
    }

    /// <summary>Navigates to a global-search result: expand its project, select the checklist, route the
    /// tab, and (for items) reveal/highlight. Never mutates order or completion state.</summary>
    private void NavigateTo(SearchResult result)
    {
        var project = Projects.FirstOrDefault(p => p.Id == result.ProjectId);
        if (project is null)
            return;
        project.IsExpanded = true;

        // A project hit opens the Project Overview rather than guessing at a checklist.
        if (result.Kind == SearchResultKind.Project)
        {
            SelectProject(project);
            return;
        }

        var checklist = result.ChecklistId is null
            ? project.Checklists.FirstOrDefault()
            : project.Checklists.FirstOrDefault(c => c.Id == result.ChecklistId);
        if (checklist is null)
        {
            SelectProject(project);   // checklist no longer exists — fall back to the overview
            return;
        }

        SelectedChecklist = checklist;

        switch (result.Kind)
        {
            case SearchResultKind.Item when result.ItemId is not null:
                checklist.RevealItem(result.ItemId, result.ItemCompleted);
                break;
            case SearchResultKind.Section:
                checklist.RevealSection();
                break;
        }
    }

    // ---------------------------------------------------------------- IWorkspaceHost

    public void OnChecklistSelected(ChecklistViewModel checklist) => SelectedChecklist = checklist;

    public void RequestDeleteProject(ProjectViewModel project)
    {
        if (!_services.Dialogs.Confirm("Delete project",
                $"Delete “{project.Name}” and all of its checklists? This cannot be undone.", "Delete"))
            return;

        var hadChecklistSelection = SelectedChecklist is not null && project.Checklists.Contains(SelectedChecklist);
        var hadOverviewSelection = ReferenceEquals(SelectedProject, project);

        ProjectService.DeleteProject(State, project.Id);
        Projects.Remove(project);

        if (hadOverviewSelection)
        {
            _selectedProject = null;
            State.Settings.LastOpenedProjectId = null;
            OnPropertyChanged(nameof(SelectedProject));
            RaiseSelectionFlags();
        }
        if (hadChecklistSelection)
            SelectedChecklist = Projects.SelectMany(p => p.Checklists).FirstOrDefault();

        RefreshWorkspaceState();
        _services.Save(immediate: true);
    }

    public void RequestDuplicateChecklist(ChecklistViewModel checklist)
    {
        var project = checklist.Project;

        // Suggest a sensible new name and let the user apply a find/replace (e.g. AHU-1 -> AHU-2).
        var input = _services.Dialogs.PromptDuplicateChecklist(checklist.Name);
        if (input is null || string.IsNullOrWhiteSpace(input.NewName))
            return;

        var copy = ChecklistService.Duplicate(
            checklist.Model, project.Model.Checklists.Select(c => c.Name),
            _services.Clock, _services.Ids,
            newName: input.NewName, find: input.Find, replace: input.Replace);

        var index = project.Model.Checklists.IndexOf(checklist.Model) + 1;
        project.Model.Checklists.Insert(Math.Clamp(index, 0, project.Model.Checklists.Count), copy);
        ProjectService.NormalizeChecklistOrders(project.Model.Checklists);

        var copyVm = new ChecklistViewModel(copy, project, _services, this);
        project.Checklists.Insert(Math.Clamp(index, 0, project.Checklists.Count), copyVm);
        SelectedChecklist = copyVm;
        _services.Save(immediate: true);
    }

    public void RequestDeleteChecklist(ChecklistViewModel checklist)
    {
        if (!_services.Dialogs.Confirm("Delete checklist",
                $"Delete “{checklist.Name}” and all of its items? This cannot be undone.", "Delete"))
            return;

        var project = checklist.Project;
        var index = project.Checklists.IndexOf(checklist);
        ProjectService.DeleteChecklist(project.Model, checklist.Id, _services.Clock);
        project.Checklists.Remove(checklist);

        if (ReferenceEquals(SelectedChecklist, checklist))
        {
            SelectedChecklist = project.Checklists.Count > 0
                ? project.Checklists[Math.Clamp(index, 0, project.Checklists.Count - 1)]
                : Projects.SelectMany(p => p.Checklists).FirstOrDefault();
        }
        _services.Save(immediate: true);
    }

    public void NotifyProjectRenamed(ProjectViewModel project)
    {
        foreach (var checklist in project.Checklists)
            checklist.RaiseBreadcrumb();
    }

    // ---------------------------------------------------------------- save status

    public bool ShowSaveStatus { get; private set; }
    public string SaveStatusText { get; private set; } = string.Empty;

    private void OnStateServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppStateService.Status))
            return;

        (ShowSaveStatus, SaveStatusText) = _services.State.Status switch
        {
            SaveState.Saving => (true, "Saving…"),
            SaveState.Saved => (true, $"Saved {DateTime.Now:h:mm tt}"),
            SaveState.Error => (true, "Save failed — check the disk"),
            _ => (false, string.Empty)
        };
        OnPropertyChanged(nameof(ShowSaveStatus));
        OnPropertyChanged(nameof(SaveStatusText));
    }

    private void RefreshWorkspaceState()
    {
        OnPropertyChanged(nameof(HasProjects));
        OnPropertyChanged(nameof(ShowWelcome));
        ExportWorkspaceCommand.RaiseCanExecuteChanged();
    }

    public void NotifyLoaded()
    {
        if (!string.IsNullOrWhiteSpace(StartupNotice))
            _services.Dialogs.ShowMessage("FieldCheck", StartupNotice!);
    }
}

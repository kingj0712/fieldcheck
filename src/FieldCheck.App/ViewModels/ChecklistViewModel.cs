using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FieldCheck.App.Behaviors;
using FieldCheck.App.Services;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.ViewModels;

/// <summary>
/// Wraps a <see cref="Checklist"/>. Holds the item view models and rebuilds the section-grouped
/// view for the current tab (Open/Completed/All) and search term.
/// </summary>
public sealed class ChecklistViewModel : BindableBase
{
    private readonly AppServices _services;
    private readonly IWorkspaceHost _host;
    private readonly Dictionary<string, ItemViewModel> _byId = new();

    public ChecklistViewModel(Checklist model, ProjectViewModel project, AppServices services, IWorkspaceHost host)
    {
        Model = model;
        Project = project;
        _services = services;
        _host = host;

        AllItems = new List<ItemViewModel>();
        foreach (var item in model.Items.OrderBy(i => i.Order))
        {
            var vm = new ItemViewModel(item, this, services);
            AllItems.Add(vm);
            _byId[item.Id] = vm;
        }

        Sections = new ObservableCollection<SectionGroupViewModel>();

        AddItemCommand = new RelayCommand(AddItem);
        AddSectionCommand = new RelayCommand(AddSection);
        ResetCommand = new RelayCommand(Reset, () => CompletedCount > 0);
        ExportCommand = new RelayCommand(Export, () => TotalCount > 0);
        PrintCommand = new RelayCommand(Print, () => TotalCount > 0);
        DuplicateCommand = new RelayCommand(() => _host.RequestDuplicateChecklist(this));
        DeleteCommand = new RelayCommand(() => _host.RequestDeleteChecklist(this));
        OpenCommand = new RelayCommand(() => _host.OnChecklistSelected(this));
        ViewCompletedCommand = new RelayCommand(() => ViewModeIndex = 1);
        SelectViewCommand = new RelayCommand<string>(v => ViewModeIndex = v switch { "completed" => 1, "all" => 2, _ => 0 });
        UndoCompleteCommand = new RelayCommand(UndoComplete);
        DismissToastCommand = new RelayCommand(DismissToast);
        ViewCompletedFromToastCommand = new RelayCommand(() => { ViewModeIndex = 1; DismissToast(); });

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) => DismissToast();

        Recompute();
    }

    public Checklist Model { get; }
    public ProjectViewModel Project { get; }
    public string Id => Model.Id;

    public List<ItemViewModel> AllItems { get; }
    public ObservableCollection<SectionGroupViewModel> Sections { get; }

    public ICommand AddItemCommand { get; }
    public ICommand AddSectionCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand PrintCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand ViewCompletedCommand { get; }
    public ICommand SelectViewCommand { get; }
    public ICommand UndoCompleteCommand { get; }
    public ICommand DismissToastCommand { get; }
    public ICommand ViewCompletedFromToastCommand { get; }

    // ---------------------------------------------------------------- name / breadcrumb

    public string Name
    {
        get => Model.Name;
        set
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                OnPropertyChanged(nameof(Name)); // revert the editor to the existing name
                return;
            }
            if (string.Equals(trimmed, Model.Name, StringComparison.Ordinal))
                return;
            ChecklistService.Rename(Model, trimmed, _services.Clock);
            OnPropertyChanged();
            OnPropertyChanged(nameof(UpdatedAtText));
            _services.Save(immediate: true);
        }
    }

    public string ProjectName => Project.Name;
    public void RaiseBreadcrumb() => OnPropertyChanged(nameof(ProjectName));

    public string UpdatedAtText => $"Updated {Model.UpdatedAt:MMM d, h:mm tt}";

    public string OpenTabLabel => $"Open ({OpenCount})";
    public string CompletedTabLabel => $"Completed ({CompletedCount})";
    public string AllTabLabel => $"All ({TotalCount})";

    // ---------------------------------------------------------------- "moved to completed" toast

    private readonly DispatcherTimer _toastTimer;
    private ItemViewModel? _undoItem;

    private bool _toastVisible;
    public bool ToastVisible
    {
        get => _toastVisible;
        private set => SetProperty(ref _toastVisible, value);
    }

    public string ToastMessage { get; private set; } = "Moved to Completed.";

    private void ShowMovedToast(ItemViewModel item)
    {
        _undoItem = item;
        ToastMessage = "Moved to Completed.";
        OnPropertyChanged(nameof(ToastMessage));
        ToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void DismissToast()
    {
        _toastTimer.Stop();
        ToastVisible = false;
        _undoItem = null;
    }

    private void UndoComplete()
    {
        var item = _undoItem;
        DismissToast();
        if (item is not null && item.IsCompleted)
            item.IsCompleted = false; // returns to Open in its preserved order (no toast on un-complete)
    }

    // ---------------------------------------------------------------- sidebar state

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string ProgressBadge => $"{CompletedCount}/{TotalCount}";

    // ---------------------------------------------------------------- view mode & search

    private int _viewModeIndex;
    public int ViewModeIndex
    {
        get => _viewModeIndex;
        set
        {
            if (!SetProperty(ref _viewModeIndex, Math.Clamp(value, 0, 2)))
                return;
            _services.State.State.Settings.LastSelectedTab = _viewModeIndex switch
            {
                1 => "completed",
                2 => "all",
                _ => "open"
            };
            OnPropertyChanged(nameof(IsOpenView));
            OnPropertyChanged(nameof(IsCompletedView));
            OnPropertyChanged(nameof(IsAllView));
            Recompute();
            _services.Save();
        }
    }

    public bool IsOpenView => _viewModeIndex == 0;
    public bool IsCompletedView => _viewModeIndex == 1;
    public bool IsAllView => _viewModeIndex == 2;

    /// <summary>Initializes the tab from saved settings without re-saving.</summary>
    public void InitViewMode(string? tab)
    {
        _viewModeIndex = tab switch { "completed" => 1, "all" => 2, _ => 0 };
        OnPropertyChanged(nameof(ViewModeIndex));
        OnPropertyChanged(nameof(IsOpenView));
        OnPropertyChanged(nameof(IsCompletedView));
        OnPropertyChanged(nameof(IsAllView));
        Recompute();
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty))
                return;
            OnPropertyChanged(nameof(IsSearching));
            OnPropertyChanged(nameof(CanReorder));
            Recompute();
        }
    }

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);
    public bool CanReorder => !IsSearching;

    // ---------------------------------------------------------------- progress

    public int TotalCount => Model.Items.Count;
    public int CompletedCount => Model.Items.Count(i => i.Completed);
    public int OpenCount => TotalCount - CompletedCount;
    public int ProgressMaximum => TotalCount == 0 ? 1 : TotalCount;
    public string ProgressDetail => $"{CompletedCount} of {TotalCount} complete";

    // ---------------------------------------------------------------- empty states / filter summary

    public bool ShowSections => Sections.Count > 0;
    public bool ShowEmptyChecklist => TotalCount == 0;
    public bool ShowNoMatches => TotalCount > 0 && IsSearching && Sections.Count == 0;
    public bool ShowNoOpen => TotalCount > 0 && !IsSearching && IsOpenView && Sections.Count == 0;
    public bool ShowNoCompleted => TotalCount > 0 && !IsSearching && IsCompletedView && Sections.Count == 0;

    public bool ShowFilterSummary => IsSearching;
    public string FilterSummary { get; private set; } = string.Empty;

    public IEnumerable<string> KnownSections =>
        Model.Items.Select(i => ChecklistService.NormalizeSection(i.Section))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                   .ToList();

    // ---------------------------------------------------------------- item operations

    private void AddItem()
    {
        var result = _services.Dialogs.PromptItem("Add item",
            new ItemEditInput(string.Empty, string.Empty, string.Empty, string.Empty), KnownSections);
        if (result is not null)
            AddItemInternal(result);
    }

    private void AddSection()
    {
        var name = _services.Dialogs.PromptText("Add section", "Section name", string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            return;
        var result = _services.Dialogs.PromptItem($"Add item to “{name.Trim()}”",
            new ItemEditInput(string.Empty, name.Trim(), string.Empty, string.Empty), KnownSections);
        if (result is not null)
            AddItemInternal(result);
    }

    private void AddItemInternal(ItemEditInput result)
    {
        var item = ChecklistService.CreateItem(result.Text, result.Section, result.Notes,
            ChecklistService.ParseTags(result.Tags), _services.Clock, _services.Ids);
        ChecklistService.AddItem(Model, item, _services.Clock);
        var vm = new ItemViewModel(item, this, _services);
        _byId[item.Id] = vm;
        AllItems.Add(vm);
        Recompute();
        _services.Save(immediate: true);
    }

    public void RemoveItem(ItemViewModel item)
    {
        if (!ChecklistService.DeleteItem(Model, item.Id, _services.Clock))
            return;
        _byId.Remove(item.Id);
        AllItems.Remove(item);
        Recompute();
        _services.Save(immediate: true);
    }

    public void DuplicateItem(ItemViewModel item)
    {
        var copy = item.Model.Clone();
        copy.Id = _services.Ids.NewId("item");
        copy.Completed = false;
        copy.CompletedAt = null;
        copy.CreatedAt = _services.Clock.Now;
        copy.UpdatedAt = _services.Clock.Now;

        var index = Model.Items.IndexOf(item.Model) + 1;
        Model.Items.Insert(Math.Clamp(index, 0, Model.Items.Count), copy);
        ChecklistService.NormalizeItemOrders(Model.Items);
        Model.UpdatedAt = _services.Clock.Now;

        var vm = new ItemViewModel(copy, this, _services);
        _byId[copy.Id] = vm;
        AllItems.Add(vm);
        Recompute();
        _services.Save(immediate: true);
    }

    public void ReorderWithinSection(SectionGroupViewModel group, ReorderRequest? request)
    {
        if (request is null || IsSearching || request.Source is not ItemViewModel source)
            return;
        // Only reorder within the same section; cross-section drops are ignored for v0.2.
        if (!string.Equals(ChecklistService.NormalizeSection(source.Model.Section), group.Section, StringComparison.Ordinal))
            return;
        var target = request.Target as ItemViewModel;
        ChecklistService.ReorderWithinSection(Model.Items, source.Model, target?.Model, request.PlaceBelow);
        Model.UpdatedAt = _services.Clock.Now;
        Recompute();
        _services.Save(immediate: true);
    }

    public void HandleItemCompletionChanged(ItemViewModel item)
    {
        Model.UpdatedAt = _services.Clock.Now;
        Recompute();
        _services.Save(immediate: true);
        if (item.IsCompleted)
            ShowMovedToast(item);
    }

    public void HandleItemEdited(ItemViewModel item)
    {
        Model.UpdatedAt = _services.Clock.Now;
        Recompute();
        _services.Save();
    }

    /// <summary>Reveals an item navigated to from global search: clears any filter, switches to the
    /// matching tab, and briefly highlights/scrolls to it (after the view has laid out).</summary>
    public void RevealItem(string itemId, bool completed)
    {
        SearchText = string.Empty;
        ViewModeIndex = completed ? 1 : 0;
        Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_byId.TryGetValue(itemId, out var vm))
                vm.Highlight();
        }), DispatcherPriority.Background);
    }

    /// <summary>Reveals a section navigated to from global search: clears any filter and shows the Open view.</summary>
    public void RevealSection()
    {
        SearchText = string.Empty;
        ViewModeIndex = 0;
    }

    private void Reset()
    {
        if (!_services.Dialogs.Confirm("Reset checklist",
                $"Mark all items in “{Model.Name}” as incomplete? This keeps the items but clears their checkmarks.",
                "Reset", destructive: false))
            return;
        ChecklistService.ResetChecklist(Model, _services.Clock);
        foreach (var vm in AllItems)
            vm.RaiseAll();
        Recompute();
        _services.Save(immediate: true);
    }

    private void Export()
    {
        var path = _services.Dialogs.SaveFile("Export checklist to CSV", SuggestFileName(Model.Name) + ".csv");
        if (path is null)
            return;
        try
        {
            File.WriteAllText(path, CsvExportService.Export(Project.Name, Model));
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Export failed", $"FieldCheck could not export the CSV:\n\n{ex.Message}");
        }
    }

    private void Print()
    {
        try
        {
            PrintService.Print(Project.Name, Model);
        }
        catch (Exception ex)
        {
            _services.Dialogs.ShowMessage("Print failed", $"FieldCheck could not print this checklist:\n\n{ex.Message}");
        }
    }

    // ---------------------------------------------------------------- recompute

    private void Recompute()
    {
        IEnumerable<ChecklistItem> source = _viewModeIndex switch
        {
            1 => ChecklistService.CompletedItems(Model),
            2 => ChecklistService.OrderedItems(Model),
            _ => ChecklistService.OpenItems(Model)
        };
        var inView = source.ToList();
        var filtered = inView.Where(Matches).ToList();
        var groups = ChecklistService.GroupBySection(filtered);

        Sections.Clear();
        foreach (var group in groups)
        {
            var sectionVm = new SectionGroupViewModel(group.Section, this);
            foreach (var item in group.Items)
                if (_byId.TryGetValue(item.Id, out var vm))
                    sectionVm.Items.Add(vm);
            Sections.Add(sectionVm);
        }

        var noun = _viewModeIndex switch { 1 => "completed items", 2 => "items", _ => "open items" };
        FilterSummary = $"Showing {filtered.Count} of {inView.Count} {noun}";

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(OpenCount));
        OnPropertyChanged(nameof(ProgressMaximum));
        OnPropertyChanged(nameof(ProgressDetail));
        OnPropertyChanged(nameof(ProgressBadge));
        OnPropertyChanged(nameof(UpdatedAtText));
        OnPropertyChanged(nameof(ShowSections));
        OnPropertyChanged(nameof(ShowEmptyChecklist));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(ShowNoOpen));
        OnPropertyChanged(nameof(ShowNoCompleted));
        OnPropertyChanged(nameof(ShowFilterSummary));
        OnPropertyChanged(nameof(FilterSummary));
        OnPropertyChanged(nameof(OpenTabLabel));
        OnPropertyChanged(nameof(CompletedTabLabel));
        OnPropertyChanged(nameof(AllTabLabel));
        ResetCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
        PrintCommand.RaiseCanExecuteChanged();
        Project.RefreshProgress();
    }

    private bool Matches(ChecklistItem item)
    {
        if (!IsSearching)
            return true;
        var term = SearchText.Trim();
        return Contains(item.Text, term)
               || Contains(item.Notes, term)
               || Contains(item.Section, term)
               || item.Tags.Any(t => Contains(t, term));
    }

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string SuggestFileName(string name)
    {
        var cleaned = name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(c, '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "checklist" : cleaned;
    }
}

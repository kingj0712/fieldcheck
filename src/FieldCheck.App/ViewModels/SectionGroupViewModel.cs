using System.Collections.ObjectModel;
using System.Windows.Input;
using FieldCheck.App.Behaviors;

namespace FieldCheck.App.ViewModels;

/// <summary>A section header plus its items, used to render the grouped checklist body.</summary>
public sealed class SectionGroupViewModel : BindableBase
{
    private readonly ChecklistViewModel _owner;

    public SectionGroupViewModel(string section, ChecklistViewModel owner)
    {
        Section = section;
        _owner = owner;
        Items = new ObservableCollection<ItemViewModel>();
        ReorderCommand = new RelayCommand<ReorderRequest>(r => _owner.ReorderWithinSection(this, r));
        ToggleCollapseCommand = new RelayCommand(() => IsCollapsed = !IsCollapsed);
    }

    public string Section { get; }

    public ObservableCollection<ItemViewModel> Items { get; }

    public string HeaderText => $"{Section}  ({Items.Count})";

    /// <summary>Within-section reordering is disabled while a search filter is active.</summary>
    public bool CanReorder => _owner.CanReorder;

    private bool _isCollapsed;
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetProperty(ref _isCollapsed, value);
    }

    public ICommand ReorderCommand { get; }
    public ICommand ToggleCollapseCommand { get; }
}

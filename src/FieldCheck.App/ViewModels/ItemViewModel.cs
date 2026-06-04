using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;
using FieldCheck.App.Services;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.ViewModels;

/// <summary>Wraps a <see cref="ChecklistItem"/> for binding. Item editing happens via a dialog.</summary>
public sealed class ItemViewModel : BindableBase
{
    private readonly AppServices _services;
    private readonly ChecklistViewModel _owner;

    public ItemViewModel(ChecklistItem model, ChecklistViewModel owner, AppServices services)
    {
        Model = model;
        _owner = owner;
        _services = services;

        EditCommand = new RelayCommand(Edit);
        DeleteCommand = new RelayCommand(Delete);
        DuplicateCommand = new RelayCommand(Duplicate);
        ToggleCommand = new RelayCommand(() => IsCompleted = !IsCompleted);
    }

    public ChecklistItem Model { get; }

    public string Id => Model.Id;
    public int Order => Model.Order;
    public string Section => Model.Section;

    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand ToggleCommand { get; }

    public bool IsCompleted
    {
        get => Model.Completed;
        set
        {
            if (Model.Completed == value)
                return;
            ChecklistService.SetItemCompleted(Model, value, _services.Clock);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowCompletedAt));
            OnPropertyChanged(nameof(CompletedAtText));
            _owner.HandleItemCompletionChanged(this);
        }
    }

    public string Text => Model.Text;

    public string Notes => Model.Notes;
    public bool HasNotes => !string.IsNullOrWhiteSpace(Model.Notes);

    public IReadOnlyList<string> Tags => Model.Tags;
    public bool HasTags => Model.Tags.Count > 0;

    public bool ShowCompletedAt => Model.Completed && Model.CompletedAt is not null;
    public string CompletedAtText =>
        Model.CompletedAt is { } when ? $"Completed {when:MMM d, yyyy} at {when:h:mm tt}" : string.Empty;

    // ---------------------------------------------------------------- transient highlight (search navigation)

    private DispatcherTimer? _highlightTimer;
    private bool _isHighlighted;
    public bool IsHighlighted
    {
        get => _isHighlighted;
        private set => SetProperty(ref _isHighlighted, value);
    }

    /// <summary>Briefly highlights the row and scrolls it into view (used when global search navigates here).</summary>
    public void Highlight()
    {
        IsHighlighted = false; // reset so re-navigating to the same item re-triggers the flash/scroll
        IsHighlighted = true;
        _highlightTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _highlightTimer.Tick -= OnHighlightElapsed;
        _highlightTimer.Tick += OnHighlightElapsed;
        _highlightTimer.Stop();
        _highlightTimer.Start();
    }

    private void OnHighlightElapsed(object? sender, EventArgs e)
    {
        _highlightTimer?.Stop();
        IsHighlighted = false;
    }

    private void Edit()
    {
        var result = _services.Dialogs.PromptItem(
            "Edit item",
            new ItemEditInput(Model.Text, Model.Section, Model.Notes, ChecklistService.FormatTags(Model.Tags)),
            _owner.KnownSections);
        if (result is null)
            return;

        ChecklistService.EditItem(Model, result.Text, result.Section, result.Notes,
            ChecklistService.ParseTags(result.Tags), _services.Clock);
        RaiseAll();
        _owner.HandleItemEdited(this);
    }

    private void Delete()
    {
        if (!_services.Dialogs.Confirm("Delete item", $"Delete “{Model.Text}”?", "Delete"))
            return;
        _owner.RemoveItem(this);
    }

    private void Duplicate() => _owner.DuplicateItem(this);

    public void RaiseAll()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Section));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(ShowCompletedAt));
        OnPropertyChanged(nameof(CompletedAtText));
        OnPropertyChanged(nameof(Order));
    }
}

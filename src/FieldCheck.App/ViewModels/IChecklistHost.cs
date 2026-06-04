namespace FieldCheck.App.ViewModels;

/// <summary>
/// Lets a <see cref="ChecklistViewModel"/> ask its parent to perform checklist-list-level actions
/// (which touch the sidebar collection and selection). Implemented by <see cref="MainViewModel"/>.
/// </summary>
public interface IChecklistHost
{
    void RequestRename(ChecklistViewModel checklist);
    void RequestDuplicate(ChecklistViewModel checklist);
    void RequestDelete(ChecklistViewModel checklist);
}

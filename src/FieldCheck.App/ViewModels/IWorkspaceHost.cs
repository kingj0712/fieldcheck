namespace FieldCheck.App.ViewModels;

/// <summary>
/// Lets project/checklist view models ask the top-level <see cref="MainViewModel"/> to perform
/// actions that touch the sidebar collections or the single global selection.
/// </summary>
public interface IWorkspaceHost
{
    /// <summary>A checklist row was chosen; make it the single global selection.</summary>
    void OnChecklistSelected(ChecklistViewModel checklist);

    void RequestNewChecklist(ProjectViewModel project);
    void RequestDeleteProject(ProjectViewModel project);

    void RequestDuplicateChecklist(ChecklistViewModel checklist);
    void RequestDeleteChecklist(ChecklistViewModel checklist);

    /// <summary>A project was renamed; refresh anything showing the project name (e.g. breadcrumbs).</summary>
    void NotifyProjectRenamed(ProjectViewModel project);
}

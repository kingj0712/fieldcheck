using FieldCheck.Core.Utilities;

namespace FieldCheck.App.Services;

/// <summary>
/// A small bundle of the services the view models share. I pass this down the view-model tree
/// instead of threading many constructor parameters through every level.
/// </summary>
public sealed class AppServices
{
    public AppServices(AppStateService state, ThemeService theme, IDialogService dialogs, IClock clock, IIdGenerator ids)
    {
        State = state;
        Theme = theme;
        Dialogs = dialogs;
        Clock = clock;
        Ids = ids;
    }

    public AppStateService State { get; }
    public ThemeService Theme { get; }
    public IDialogService Dialogs { get; }
    public IClock Clock { get; }
    public IIdGenerator Ids { get; }

    /// <summary>Convenience pass-through to the autosave coordinator.</summary>
    public void Save(bool immediate = false) => State.RequestSave(immediate);
}

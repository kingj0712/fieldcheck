using System.ComponentModel;
using System.Windows.Threading;
using FieldCheck.Core.Models;
using FieldCheck.Core.Services;

namespace FieldCheck.App.Services;

public enum SaveState
{
    Idle,
    Saving,
    Saved,
    Error
}

/// <summary>
/// Owns the live <see cref="AppState"/> and debounces autosaves. Major actions can save
/// immediately; text edits and drags request a debounced save so we don't hammer the disk.
/// </summary>
public sealed class AppStateService : INotifyPropertyChanged
{
    private readonly IFileStorageService _storage;
    private readonly DispatcherTimer _debounce;

    public AppStateService(IFileStorageService storage)
    {
        _storage = storage;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _debounce.Tick += (_, _) => SaveNow();
        State = new AppState();
    }

    public AppState State { get; private set; }

    public string DataFilePath => _storage.DataFilePath;

    private SaveState _status = SaveState.Idle;
    public SaveState Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public string? LastError { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Loads the previous session (or an empty/recovered state) and returns the outcome.</summary>
    public LoadResult Load()
    {
        var result = _storage.Load();
        State = result.State;
        Status = SaveState.Idle;
        return result;
    }

    /// <summary>
    /// Requests a save. <paramref name="immediate"/> writes now (for structural/major changes);
    /// otherwise the write is debounced so rapid edits collapse into one disk write.
    /// </summary>
    public void RequestSave(bool immediate = false)
    {
        if (immediate)
        {
            SaveNow();
            return;
        }

        Status = SaveState.Saving;
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Flushes any pending save synchronously (used on app shutdown).</summary>
    public void Flush()
    {
        if (_debounce.IsEnabled)
            SaveNow();
    }

    private void SaveNow()
    {
        _debounce.Stop();
        try
        {
            _storage.Save(State);
            LastError = null;
            Status = SaveState.Saved;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Status = SaveState.Error;
        }
    }

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

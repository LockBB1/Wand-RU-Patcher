using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using WandEnhancer.ReactiveUICore;
using WandRuInstaller.Core;
using WandRuInstaller.Models;

namespace WandRuInstaller.ViewModels;

public sealed class MainVm : ObservableObject
{
    InstallerState _state = InstallerState.Detecting;
    string _statusText = "";
    readonly RuOverrides _overrides = RuOverrides.LoadEmbedded();

    public InstallerState State { get => _state; private set => SetProperty(ref _state, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public ObservableCollection<string> Log { get; } = new();
    public WandInstall? Install { get; private set; }

    public ICommand PatchCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand BrowseCommand { get; }

    public MainVm()
    {
        PatchCommand = new AsyncRelayCommand(async _ => await PatchAsync(),
            _ => State is InstallerState.Ready or InstallerState.Patched or InstallerState.Done or InstallerState.Error);
        RestoreCommand = new AsyncRelayCommand(async _ => await RestoreAsync(),
            _ => State is InstallerState.Patched);
        BrowseCommand = new RelayCommand(p => { if (p is string dir) DetectFrom(new[] { dir }); });
    }

    public void Detect() => DetectFrom(WandLocator.DefaultRoots());

    public void DetectFrom(IEnumerable<string> roots)
    {
        State = InstallerState.Detecting;
        Install = WandLocator.Detect(roots);
        if (Install is null)
        {
            State = InstallerState.NotFound;
            StatusText = "Wand не найден";
            return;
        }
        var ver = new DirectoryInfo(Install.SelectedAppDir!).Name.Replace("app-", "");
        State = Install.IsPatched ? InstallerState.Patched : InstallerState.Ready;
        StatusText = $"Wand {ver}";
    }

    internal async Task PatchAsync()
    {
        if (Install?.SelectedAppDir is null) return;
        State = InstallerState.Working;
        Log.Clear();
        try
        {
            await Task.Run(() => new RuPatcher(Install.SelectedAppDir, _overrides, Add).Apply());
            State = InstallerState.Done;
            StatusText = "Готово! Перезапустите Wand → Настройки → Язык → Русский";
            if (Install is not null) Install.IsPatched = true;
        }
        catch (Exception ex)
        {
            State = InstallerState.Error;
            StatusText = "Ошибка: " + ex.Message;
            Add(ex.ToString());
        }
    }

    internal async Task RestoreAsync()
    {
        if (Install?.SelectedAppDir is null) return;
        var root = Install.RootDir;
        State = InstallerState.Working;
        Log.Clear();
        try
        {
            await Task.Run(() => RuUnpatcher.Restore(Install.SelectedAppDir, Add));
            DetectFrom(new[] { root });
        }
        catch (Exception ex)
        {
            State = InstallerState.Error;
            StatusText = "Ошибка: " + ex.Message;
            Add(ex.ToString());
        }
    }

    void Add(string message)
    {
        var app = System.Windows.Application.Current;
        if (app is not null) app.Dispatcher.Invoke(() => Log.Add(message));
        else Log.Add(message);
    }
}

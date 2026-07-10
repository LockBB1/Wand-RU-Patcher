using System.Collections.ObjectModel;
using System.Diagnostics;
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
    bool _isSettingsOpen;
    SettingsVm? _settings;
    readonly RuOverrides _overrides = RuOverrides.LoadEmbedded();

    public InstallerState State { get => _state; private set => SetProperty(ref _state, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public ObservableCollection<string> Log { get; } = new();
    public WandInstall? Install { get; private set; }
    public SettingsVm? Settings { get => _settings; private set => SetProperty(ref _settings, value); }
    public bool IsSettingsOpen { get => _isSettingsOpen; set => SetProperty(ref _isSettingsOpen, value); }

    public ICommand PatchCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }

    public MainVm()
    {
        PatchCommand = new AsyncRelayCommand(async _ => await PatchAsync(),
            _ => State is InstallerState.Ready or InstallerState.Patched or InstallerState.Done or InstallerState.Error);
        RestoreCommand = new AsyncRelayCommand(async _ => await RestoreAsync(),
            _ => State is InstallerState.Patched);
        BrowseCommand = new RelayCommand(p => { if (p is string dir) DetectFrom(new[] { dir }); });
        OpenSettingsCommand = new RelayCommand(_ => IsSettingsOpen = true, _ => Settings is not null);
        CloseSettingsCommand = new RelayCommand(_ => IsSettingsOpen = false);
    }

    public void Detect() => DetectFrom(WandLocator.DefaultRoots());

    /// <summary>Ручной выбор папки: пробуем саму папку и её подпапки (вдруг выбрали родителя Wand).</summary>
    public void DetectFromFolder(string folder)
    {
        var roots = new List<string> { folder };
        try { roots.AddRange(Directory.GetDirectories(folder)); } catch { /* нет доступа */ }
        DetectFrom(roots);
    }

    public void DetectFrom(IEnumerable<string> roots)
    {
        State = InstallerState.Detecting;
        Install = WandLocator.Detect(roots);
        if (Install is null)
        {
            State = InstallerState.NotFound;
            StatusText = L.Get("S_Msg_NotFound");
            return;
        }
        Settings = new SettingsVm(Install);
        var ver = new DirectoryInfo(Install.SelectedAppDir!).Name.Replace("app-", "");
        State = Install.IsPatched ? InstallerState.Patched : InstallerState.Ready;
        StatusText = $"Wand {ver}";
    }

    internal async Task PatchAsync()
    {
        if (Install?.SelectedAppDir is null) return;
        if (!await EnsureWandClosedAsync()) return;
        State = InstallerState.Working;
        Log.Clear();
        try
        {
            await Task.Run(() => new RuPatcher(Install.SelectedAppDir, _overrides, Add).Apply());
            State = InstallerState.Done;
            StatusText = L.Get("S_Msg_Done");
            if (Install is not null) Install.IsPatched = true;
            if (Settings?.RestartWandAfter == true) TryRestartWand();
        }
        catch (Exception ex)
        {
            State = InstallerState.Error;
            StatusText = L.Get("S_Msg_ErrorPrefix") + ex.Message;
            Add(ex.ToString());
        }
    }

    internal async Task RestoreAsync()
    {
        if (Install?.SelectedAppDir is null) return;
        if (!await EnsureWandClosedAsync()) return;
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
            StatusText = L.Get("S_Msg_ErrorPrefix") + ex.Message;
            Add(ex.ToString());
        }
    }

    // Wand залочивает файлы (app.asar.unpacked/*.exe). Если включён авто-перезапуск — закрываем сами,
    // иначе просим юзера закрыть. Возвращает false, если продолжать нельзя.
    async Task<bool> EnsureWandClosedAsync()
    {
        if (!WandProcess.AnyRunning()) return true;
        if (Settings?.RestartWandAfter != true)
        {
            StatusText = L.Get("S_Msg_WandRunning");
            return false;
        }
        WandProcess.KillAll();
        await Task.Delay(700); // дать ОС снять блокировки файлов
        return true;
    }

    // Продуктовая фича: перезапуск Wand для конечного юзера. В dev-тестах RestartWandAfter=false.
    void TryRestartWand()
    {
        try
        {
            var exe = Path.Combine(Install!.RootDir, "Wand.exe");
            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception ex) { Add("Не удалось перезапустить Wand: " + ex.Message); }
    }

    void Add(string message)
    {
        var app = System.Windows.Application.Current;
        if (app is not null) app.Dispatcher.Invoke(() => Log.Add(message));
        else Log.Add(message);
    }
}

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
    string _migrationHint = "";
    bool _isAboutOpen;
    bool _isHelpOpen;
    SettingsVm? _settings;
    readonly RuOverrides _overrides = RuOverrides.LoadEmbedded();

    public InstallerState State { get => _state; private set => SetProperty(ref _state, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    /// <summary>Wand обновился, патч остался в старой app-версии - подсказка «перенесите» (пусто = скрыта).</summary>
    public string MigrationHint { get => _migrationHint; private set => SetProperty(ref _migrationHint, value); }
    public ObservableCollection<string> Log { get; } = new();
    public WandInstall? Install { get; private set; }
    public SettingsVm? Settings { get => _settings; private set => SetProperty(ref _settings, value); }
    public bool IsAboutOpen { get => _isAboutOpen; set => SetProperty(ref _isAboutOpen, value); }
    public bool IsHelpOpen { get => _isHelpOpen; set => SetProperty(ref _isHelpOpen, value); }

    public ICommand PatchCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand OpenAboutCommand { get; }
    public ICommand CloseAboutCommand { get; }
    public ICommand OpenHelpCommand { get; }
    public ICommand CloseHelpCommand { get; }

    public MainVm()
    {
        PatchCommand = new AsyncRelayCommand(async p => await PatchAsync(p as string),
            _ => State is InstallerState.Ready or InstallerState.Patched or InstallerState.Done or InstallerState.Error);
        RestoreCommand = new AsyncRelayCommand(async _ => await RestoreAsync(),
            _ => State is InstallerState.Patched);
        BrowseCommand = new RelayCommand(p => { if (p is string dir) DetectFrom(new[] { dir }); });
        OpenAboutCommand = new RelayCommand(_ => IsAboutOpen = true);
        CloseAboutCommand = new RelayCommand(_ => IsAboutOpen = false);
        OpenHelpCommand = new RelayCommand(_ => IsHelpOpen = true);
        CloseHelpCommand = new RelayCommand(_ => IsHelpOpen = false);
    }

    MapDiagServer? _mapDiag;

    /// <summary>PoC Шаг 1: поднять приёмник диагностики map-хука (Wand шлёт по o.net в лог). Один раз.</summary>
    public void StartMapDiag()
    {
        if (_mapDiag is not null) return;
        _mapDiag = new MapDiagServer(line => Add("[map] " + line));
        if (_mapDiag.Start()) Add($"[map] диагностика карт слушает :{MapDiagServer.Port} - открой карту в Wand");
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
        Install = WandLocator.Detect(roots, AppSettings.Load().PinnedAppVersion);
        if (Install is null)
        {
            State = InstallerState.NotFound;
            StatusText = L.Get("S_Msg_NotFound");
            return;
        }
        Settings = new SettingsVm(Install);
        Settings.OnAppDirSelected = _ => RefreshSelection();   // смена версии в настройках -> обновить шапку/состояние
        RefreshSelection();
    }

    /// <summary>Пересчитать состояние/шапку/подсказку из Install.SelectedAppDir (после Detect или смены версии).</summary>
    void RefreshSelection()
    {
        if (Install?.SelectedAppDir is null) return;
        var man = WandLocator.Manifest(Install.SelectedAppDir);
        Install.IsPatched = man is not null;
        Install.Manifest = man;
        var ver = WandLocator.VersionOf(Install.SelectedAppDir);
        var pinned = ver != WandLocator.VersionOf(Install.AppDirs[0]);   // выбрана не последняя -> закреплена
        State = Install.IsPatched ? InstallerState.Patched : InstallerState.Ready;
        StatusText = pinned ? $"Wand {ver} (закреплено)" : $"Wand {ver}";
        MigrationHint = Install.PatchedOtherAppDir is null
            ? ""
            : string.Format(L.Get("S_MigrateHint"), ver, WandLocator.VersionOf(Install.PatchedOtherAppDir));
    }

    // mode: "local" -> офлайн-перевод, "online" -> +интернет, null -> оставить текущий (переустановка).
    internal async Task PatchAsync(string? mode = null)
    {
        if (Install?.SelectedAppDir is null) return;
        if (Settings is not null && mode is not null)
            Settings.TranslateCheatsOnline = mode.Equals("online", StringComparison.OrdinalIgnoreCase);
        if (!await EnsureWandClosedAsync()) return;
        State = InstallerState.Working;
        Log.Clear();
        try
        {
            var translateCheats = Settings?.TranslateCheats ?? true;
            await Task.Run(() => new RuPatcher(Install.SelectedAppDir, _overrides, translateCheats, Add).Apply());
            State = InstallerState.Done;
            StatusText = L.Get("S_Msg_Done");
            if (Install is not null) Install.IsPatched = true;
            MigrationHint = ""; // патч теперь в актуальной версии
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

    // Wand залочивает файлы (app.asar.unpacked/*.exe). Если включён авто-перезапуск - закрываем сами,
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

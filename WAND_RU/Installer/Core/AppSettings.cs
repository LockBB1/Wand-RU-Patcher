using System.IO;
using System.Text.Json;

namespace WandRuInstaller.Core;

/// <summary>Настройки установщика, сохраняются в %AppData%\WandRuInstaller\settings.json.</summary>
public sealed class AppSettings
{
    public bool RestartWandAfter { get; set; }
    public bool ShowLog { get; set; }
    public bool TranslateCheats { get; set; } = true;
    public bool TranslateCheatsOnline { get; set; }
    /// <summary>Перевод игровых карт: встраивать map-хук (офлайн-словарь + шаблоны фильтров).</summary>
    public bool TranslateMaps { get; set; } = true;
    /// <summary>Онлайн-добор карт: Google gtx -> MyMemory на остаток (POI-описания вне словаря).
    /// Opt-in (как читы): офлайн-словарь+шаблоны и так покрывают почти весь UI, без риска 429.</summary>
    public bool TranslateMapsOnline { get; set; }
    /// <summary>Провайдер онлайн-MT: "auto" (Google -> MyMemory), "google", "mymemory". Читает хук.</summary>
    public string OnlineProvider { get; set; } = "auto";
    /// <summary>Закреплённая версия Wand ("12.37.0"): патчим/показываем её, а не последнюю. Пусто = последняя.
    /// Даёт играть на старой версии, пока WRP не поддержал новую.</summary>
    public string? PinnedAppVersion { get; set; }

    static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WandRuInstaller");
    static string DefaultPath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load() => Load(DefaultPath);
    public void Save() => Save(DefaultPath);

    internal static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch { /* битый/недоступный файл - дефолты */ }
        return new AppSettings();
    }

    internal void Save(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* не удалось сохранить - не критично */ }
    }
}

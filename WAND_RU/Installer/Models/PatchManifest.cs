namespace WandRuInstaller.Models;

public sealed class PatchManifest
{
    public string Name { get; set; } = "Wand RU";
    public string PatchVersion { get; set; } = "0.1.0";
    public int Build { get; set; }
    public string AppVersion { get; set; } = "";
    public string InstalledAt { get; set; } = "";
    public string BackupRoot { get; set; } = "";
}

namespace WandRuInstaller.Models;

public sealed class WandInstall
{
    public required string RootDir { get; init; }
    public required IReadOnlyList<string> AppDirs { get; init; } // абсолютные пути app-*
    public string? SelectedAppDir { get; set; }
    public bool IsPatched { get; set; }
    public PatchManifest? Manifest { get; set; }
}

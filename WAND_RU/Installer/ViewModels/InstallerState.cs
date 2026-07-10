namespace WandRuInstaller.ViewModels;

public enum InstallerState
{
    Detecting,
    Ready,      // Wand найден, не пропатчен
    Patched,    // Wand найден, пропатчен
    NotFound,   // Wand не найден
    Working,    // идёт патч/откат
    Done,       // патч успешно применён
    Error,      // ошибка
}

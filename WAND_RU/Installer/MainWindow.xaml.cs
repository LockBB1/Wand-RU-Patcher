using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WandRuInstaller.Core;
using WandRuInstaller.ViewModels;

namespace WandRuInstaller;

public partial class MainWindow : Window
{
    const string RepoUrl = "https://github.com/LockBB1/Wand-RU-Patcher";

    public MainVm ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var ver = info?.Split('+')[0];
        var verText = string.IsNullOrEmpty(ver) ? "" : $"v {ver}";
        VersionLabel.Text = verText;
        AboutVersion.Text = verText;
        Loaded += (_, _) =>
        {
            ViewModel.Detect();
            _ = ShowUpdateBannerAsync(ver);
        };
    }

    // Тихая проверка обновления: офлайн/ошибка - баннер просто не показываем.
    async Task ShowUpdateBannerAsync(string? current)
    {
        if (string.IsNullOrEmpty(current)) return;
        var latest = await UpdateChecker.CheckAsync(current);
        if (latest is null) return;
        UpdateBanner.Text = string.Format(L.Get("S_UpdateAvailable"), latest);
        UpdateBanner.Visibility = Visibility.Visible;
    }

    void OnOpenReleases(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // не отдавать клик OnDragMove шапки - иначе DragMove съедает его
        try { Process.Start(new ProcessStartInfo(UpdateChecker.ReleasesUrl) { UseShellExecute = true }); }
        catch { /* нет браузера */ }
    }

    void OnOpenSource(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true }); }
        catch { /* нет браузера */ }
    }

    void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void OnClose(object sender, RoutedEventArgs e) => Close();

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = L.Get("S_Browse_Dialog") };
        if (dialog.ShowDialog(this) != true) return;
        ViewModel.DetectFromFolder(dialog.FolderName);
    }

    void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Settings is null) return;
        new SettingsWindow(ViewModel) { Owner = this }.ShowDialog();
    }

    void OnCopyLog(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(BuildLogReport()); } catch { /* clipboard busy */ }
    }

    void OnExportLog(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            FileName = "wand-ru-log.md",
            Filter = "Markdown (*.md)|*.md|Текст (*.txt)|*.txt",
            Title = L.Get("S_ExportLog"),
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var plain = dialog.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
            File.WriteAllText(dialog.FileName,
                plain ? string.Join(Environment.NewLine, ViewModel.Log) : BuildLogReport(),
                new UTF8Encoding(false));
        }
        catch { /* запись не удалась */ }
    }

    string BuildLogReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Wand RU - лог");
        sb.AppendLine();
        sb.AppendLine($"- WRP: {VersionLabel.Text}");
        var app = ViewModel.Install?.SelectedAppDir;
        if (app is not null) sb.AppendLine($"- Wand: {Path.GetFileName(app)}");
        sb.AppendLine($"- OS: {Environment.OSVersion}");
        sb.AppendLine();
        sb.AppendLine("```");
        foreach (var line in ViewModel.Log) sb.AppendLine(line);
        sb.AppendLine("```");
        return sb.ToString();
    }
}

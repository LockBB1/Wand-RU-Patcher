using System.Globalization;
using System.Windows;
using WandRuInstaller.Core;
using WandRuInstaller.Core.Runtime;

namespace WandRuInstaller;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        var ru = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
        var uri = new Uri($"Locale/lang.{(ru ? "ru-RU" : "en-US")}.xaml", UriKind.Relative);
        Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = uri });
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() == "--runtime-launch")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                if (e.Args.Length < 2 || e.Args.Length > 2 && e.Args[2] != "--") throw new ArgumentException("Не указана папка или аргументы Wand.");
                var settings = AppSettings.Load();
                var directories = WandLocator.FindAppDirs(e.Args[1]);
                var appDir = directories.FirstOrDefault(d => WandLocator.VersionOf(d) == settings.PinnedAppVersion)
                    ?? directories.FirstOrDefault() ?? throw new InvalidOperationException("Установка Wand не найдена.");
                await RuntimeHost.RunAsync(RuntimeHost.CreateStartInfo(appDir, e.Args.Skip(3)), settings);
                Shutdown();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Wand RU", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}

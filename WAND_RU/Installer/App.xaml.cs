using System.Globalization;
using System.Windows;

namespace WandRuInstaller;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var ru = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
        var uri = new Uri($"Locale/lang.{(ru ? "ru-RU" : "en-US")}.xaml", UriKind.Relative);
        Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = uri });
        base.OnStartup(e);
    }
}

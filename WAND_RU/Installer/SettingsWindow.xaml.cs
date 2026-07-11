using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WandRuInstaller.Core;
using WandRuInstaller.ViewModels;

namespace WandRuInstaller;

/// <summary>Отдельное окно настроек (раньше был оверлей в главном окне). DataContext = MainVm.</summary>
public partial class SettingsWindow : Window
{
    readonly MainVm _vm;

    public SettingsWindow(MainVm vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
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
        _vm.DetectFromFolder(dialog.FolderName); // пересоздаёт Settings — закрываем окно
        Close();
    }
}

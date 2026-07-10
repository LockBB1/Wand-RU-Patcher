using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WandRuInstaller.Core;
using WandRuInstaller.ViewModels;

namespace WandRuInstaller;

public partial class MainWindow : Window
{
    public MainVm ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = v is null ? "" : $"v {v.Major}.{v.Minor}.{v.Build}";
        Loaded += (_, _) => ViewModel.Detect();
    }

    void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void OnClose(object sender, RoutedEventArgs e) => Close();

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = L.Get("S_Browse_Dialog") };
        if (dialog.ShowDialog(this) == true)
            ViewModel.DetectFrom(new[] { dialog.FolderName });
    }
}

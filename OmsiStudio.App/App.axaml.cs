using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using OmsiStudio.App.ViewModels;
using OmsiStudio.App.Views;
using OmsiStudio.App.Services;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;

namespace OmsiStudio.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var parser = new ScoFileParser();
            var directoryScanner = new OmsiDirectoryScanner();
            var scanner = new OmsiAssetScanner(parser, directoryScanner);
            var folderPickerService = new AvaloniaFolderPickerService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(scanner, folderPickerService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
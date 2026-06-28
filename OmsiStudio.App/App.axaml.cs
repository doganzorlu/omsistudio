using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OmsiStudio.App.ViewModels;
using OmsiStudio.App.Views;
using OmsiStudio.App.Services;
using OmsiStudio.App.Services.Rendering;
using OmsiStudio.OmsiFormat.Parser;
using OmsiStudio.OmsiFormat.Scanner;
using OmsiStudio.Core.Services;

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
            var appSettingsService = new JsonAppSettingsService();
            var uiDispatcher = new AvaloniaUiDispatcher();
            var rendererHost = new OpenGlRendererHost();
            var geometryReader = new O3dGeometryReader();
            var boundsCalculator = new MeshBoundsCalculator();
            var localizationService = new LocalizationService();
            var previewLoader = new AssetPreviewLoader(geometryReader, boundsCalculator, localizationService);

            var textureResolver = new OmsiTextureReferenceResolver();
            var textureLoader = new TextureImageLoader();
            var textureCache = new TextureImageCacheService(textureLoader);
            var textureBindingService = new MaterialTextureBindingService(textureResolver, textureCache);

            var viewModel = new MainWindowViewModel(
                scanner,
                folderPickerService,
                appSettingsService,
                new AvaloniaClipboardService(),
                new ProcessFileLauncherService(),
                localizationService,
                uiDispatcher: uiDispatcher,
                scanCacheService: new JsonScanCacheService(),
                previewLoader: previewLoader,
                rendererHost: rendererHost,
                materialTextureBindingService: textureBindingService);
            _ = viewModel.LoadSettingsAsync();

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

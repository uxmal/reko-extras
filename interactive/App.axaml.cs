using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Reko.Core.Configuration;
using Reko.Core.Services;
using Reko.Extras.Interactive.ViewModels;
using Reko.Extras.Interactive.Views;
using Reko.Services;
using System.ComponentModel.Design;

namespace Reko.Extras.Interactive;

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
            var sc = new System.ComponentModel.Design.ServiceContainer();
            var progress = new ProgressIndicator();
            var diagnosticsViewModel = new DiagnosticsViewModel(progress);
            sc.AddService<IDecompilerEventListener>(diagnosticsViewModel);
            sc.AddService<IEventListener>(diagnosticsViewModel);
            sc.AddService<IFileSystemService>(new FileSystemService());
            sc.AddService<IPluginLoaderService>(new PluginLoaderService());
            sc.AddService<IConfigurationService>(RekoConfigurationService.Load(sc, "reko/reko.config"));
            var decompilerHost = new DecompilerHost(diagnosticsViewModel);
            var rwhost = new RewriterHost(diagnosticsViewModel);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel
                {
                    CodeView = new CodeViewModel(sc, decompilerHost, rwhost),
                    Diagnostics = diagnosticsViewModel,
                    Progress = progress
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
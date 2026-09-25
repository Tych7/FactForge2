using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using FactForge.Services;
using FactForge.ViewModels;
using FactForge.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FactForge;

public partial class App : Application
{
    public static LocalWebHost? WebHost { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var services = WebHost?.Services ?? throw new InvalidOperationException("Web host not started.");
            var mainWindowViewModel = new MainWindowViewModel(
                services.GetRequiredService<QuizRepository>(),
                services.GetRequiredService<PresentationService>(),
                services.GetRequiredService<QrCodeService>(),
                services.GetRequiredService<IDialogService>());

            desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}

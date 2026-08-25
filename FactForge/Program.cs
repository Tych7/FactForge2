using System;
using Avalonia;
using FactForge.Services;

namespace FactForge;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var webHost = new LocalWebHost();
        webHost.StartAsync().GetAwaiter().GetResult();
        App.WebHost = webHost;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            webHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

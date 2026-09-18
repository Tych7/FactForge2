using System;
using System.Threading.Tasks;
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
            // Run on a plain thread-pool thread: the Avalonia dispatcher's
            // SynchronizationContext is still installed on this thread even though its
            // message loop has stopped, so any await inside DisposeAsync (ours or ASP.NET
            // Core's internals) that tries to resume on it would hang forever.
            Task.Run(() => webHost.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

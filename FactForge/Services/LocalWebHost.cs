using System;
using System.Threading;
using System.Threading.Tasks;
using FactForge.Data;
using FactForge.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FactForge.Services;

// Composition root for the whole app: builds one DI container that serves both the
// embedded web server (player-facing SignalR hub + static files) and the Avalonia
// UI, so both sides share the same PresentationService/QuizRepository/DbContext
// factory singletons. Mirrors the "host Kestrel inside the desktop app" pattern
// from the V1 prototype, generalized into a proper DI-backed service.
public class LocalWebHost : IAsyncDisposable
{
    public const int Port = 5187;

    private WebApplication? _app;

    public IServiceProvider Services => _app?.Services ?? throw new InvalidOperationException("Web host not started.");

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = "wwwroot"
        });
        builder.Logging.ClearProviders();

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(DbPath.GetConnectionString()));
        builder.Services.AddSingleton<QuizRepository>();
        builder.Services.AddSingleton<PresentationService>();
        builder.Services.AddSingleton<QrCodeService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSignalR();

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapHub<QuizHub>("/quizhub");

        app.Urls.Add($"http://0.0.0.0:{Port}");

        using (var scope = app.Services.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync().ConfigureAwait(false);
            await db.Database.MigrateAsync().ConfigureAwait(false);
        }

        await app.StartAsync().ConfigureAwait(false);
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is null) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _app.StopAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }
}

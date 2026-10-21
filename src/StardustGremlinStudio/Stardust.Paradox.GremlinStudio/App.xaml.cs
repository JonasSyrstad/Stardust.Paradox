using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Stardust.Paradox.GremlinStudio.Core;
using Stardust.Paradox.GremlinStudio.Core.Storage;
using Stardust.Paradox.GremlinStudio.Core.Updates;
using Stardust.Paradox.GremlinStudio.Services;
using Stardust.Paradox.GremlinStudio.ViewModels;
using Velopack;

namespace Stardust.Paradox.GremlinStudio;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private ThemeService? _themeService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Initialize Velopack as early as possible
        VelopackApp.Build().Run();

        // Migrate any legacy settings that may have been stored in the app directory
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        LegacySettingsMigration.MigrateIfNeeded(loggerFactory.CreateLogger<App>());

        // Show splash screen
        var splash = new SplashScreen();
        splash.Show();
        splash.UpdateStatus("Initializing theme...");

        // Initialize theme before building the host
        _themeService = new ThemeService();
        _themeService.Initialize();

        splash.UpdateStatus("Building services...");

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .ConfigureServices(services =>
            {
                services.AddGremlinStudioCore();

                // Register theme service
                services.AddSingleton<IThemeService>(_themeService);
                
                // Register update service
                services.AddSingleton<IUpdateService, UpdateService>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        splash.UpdateStatus("Starting host...");
        await _host.StartAsync().ConfigureAwait(false);

        splash.UpdateStatus("Loading main window...");

        // Small delay to show the splash screen
        await Task.Delay(2000).ConfigureAwait(false);

        await Dispatcher.InvokeAsync(() =>
        {
            var window = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            splash.Close();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            // Bounded, synchronous stop: async void OnExit lets the process exit mid-stop
            // and a hosted service that ignores cancellation could otherwise hang shutdown.
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                Task.Run(() => _host.StopAsync(cts.Token)).Wait(TimeSpan.FromSeconds(4));
            }
            catch (AggregateException)
            {
            }
            _host.Dispose();
        }

        base.OnExit(e);
    }
}


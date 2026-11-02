using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MuseWiser.Models;
using MuseWiser.Models.Enums;
using MuseWiser.Models.Weather;
using MuseWiser.Services.Audio;
using MuseWiser.Services.Audio.PlayMuse;
using MuseWiser.Services.Interop;
using MuseWiser.Services.Settings;
using MuseWiser.Services.Weather;
using MuseWiser.ViewModels;

namespace MuseWiser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost? _host;
        private TaskbarIcon? _trayIcon;
        private ILogger<App>? _logger;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                _host = Host.CreateDefaultBuilder()
                    .ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                        config.AddEnvironmentVariables();
                    })
                    .ConfigureServices(ConfigureServices)
                    .Build();

                await _host.StartAsync();

                _logger = _host.Services.GetRequiredService<ILogger<App>>();

                _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;

                var mainWindow = _host.Services.GetRequiredService<MainWindow>();

                var settingsService = _host.Services.GetRequiredService<ISettingsService>();
                var displaySettings = settingsService.Load();
                mainWindow.SourceInitialized += (_, _) => WindowPositionHelper.Apply(mainWindow, displaySettings);

                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "アプリケーションの起動に失敗しました。");
                MessageBox.Show($"アプリケーションの起動に失敗しました。\n{ex.Message}", "MuseWiser", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ClockViewModel>();

            services.Configure<WeatherSettings>(context.Configuration.GetSection("Weather"));
            services.AddHttpClient<IWeatherService, OpenWeatherService>();
            services.AddSingleton<WeatherViewModel>();

            services.Configure<PlayMusePipeOptions>(context.Configuration.GetSection("AudioSource"));
            services.AddSingleton<WasapiLoopbackAudioService>();
            services.AddSingleton<PlayMuseNamedPipeAudioCaptureService>();
            services.AddSingleton<IAudioCaptureService>(sp =>
            {
                var mode = sp.GetRequiredService<IOptions<PlayMusePipeOptions>>().Value.Mode;
                return mode switch
                {
                    AudioSourceMode.PlayMusePipe => sp.GetRequiredService<PlayMuseNamedPipeAudioCaptureService>(),
                    _ => sp.GetRequiredService<WasapiLoopbackAudioService>(),
                };
            });
            services.AddSingleton<VolumeMeterViewModel>();

            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SettingsWindow>();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_host is null)
            {
                return;
            }

            try
            {
                var settingsWindow = _host.Services.GetRequiredService<SettingsWindow>();
                var viewModel = (SettingsViewModel)settingsWindow.DataContext;
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();

                viewModel.SettingsSaved += (_, settings) => WindowPositionHelper.Apply(mainWindow, settings);

                settingsWindow.Owner = mainWindow;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "設定画面の表示に失敗しました。");
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Shutdown();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                _trayIcon?.Dispose();

                if (_host is not null)
                {
                    var mainViewModel = _host.Services.GetService<MainViewModel>();
                    if (mainViewModel is not null)
                    {
                        await mainViewModel.DisposeAsync();
                    }

                    await _host.StopAsync();
                    _host.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "アプリケーション終了処理中にエラーが発生しました。");
            }
            finally
            {
                DispatcherUnhandledException -= OnDispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            }

            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogCritical(e.Exception, "UI スレッドで未処理の例外が発生しました。");
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                _logger?.LogCritical(ex, "アプリケーションドメインで未処理の例外が発生しました。");
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "監視されていないタスク例外が発生しました。");
            e.SetObserved();
        }
    }
}

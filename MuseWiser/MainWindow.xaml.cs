using System.Windows;
using System.Windows.Interop;
using MuseWiser.Services.Interop;
using MuseWiser.Services.Settings;
using MuseWiser.ViewModels;

namespace MuseWiser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel, ISettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _viewModel = viewModel;
            DataContext = viewModel;

            _viewModel.Weather.PropertyChanged += Weather_PropertyChanged;
        }

        private void Weather_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WeatherViewModel.HasData))
            {
                WindowPositionHelper.Apply(this, _settingsService.Load(), _viewModel.Weather.HasData);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var windowHandle = new WindowInteropHelper(this).Handle;
            WindowClickThroughHelper.MakeClickThrough(windowHandle);
            WindowClickThroughHelper.ExcludeFromAltTab(windowHandle);
        }
    }
}
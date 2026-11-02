using System.Windows;
using System.Windows.Interop;
using MuseWiser.Services.Interop;
using MuseWiser.ViewModels;

namespace MuseWiser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var windowHandle = new WindowInteropHelper(this).Handle;
            WindowClickThroughHelper.MakeClickThrough(windowHandle);
        }
    }
}
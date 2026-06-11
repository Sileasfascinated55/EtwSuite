using EtwSuite.Etw;
using EtwSuite.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EtwSuite
{
    public sealed partial class MainWindow : Window
    {
        private readonly CancellationTokenSource _loadCancellation = new();

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new ProvidersViewModel(new EtwProviderCatalog());
            Root.DataContext = ViewModel;

            Closed += MainWindow_Closed;
        }

        public ProvidersViewModel ViewModel { get; }

        private async void Root_Loaded(object sender, RoutedEventArgs e)
        {
            Root.Loaded -= Root_Loaded;

            try
            {
                await ViewModel.LoadProvidersAsync(_loadCancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchText = ((TextBox)sender).Text;
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _loadCancellation.Cancel();
            _loadCancellation.Dispose();
        }
    }
}

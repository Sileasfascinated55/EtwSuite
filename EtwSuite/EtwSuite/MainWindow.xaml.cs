using EtwSuite.Core;
using EtwSuite.Etw;
using EtwSuite.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.ComponentModel;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace EtwSuite
{
    public sealed partial class MainWindow : Window
    {
        private readonly CancellationTokenSource _loadCancellation = new();
        private CancellationTokenSource? _schemaLoadCancellation;

        public MainWindow()
        {
            InitializeComponent();

            var providerCatalog = new EtwProviderCatalog();
            ProvidersViewModel = new ProvidersViewModel(providerCatalog);
            ConsumeProviderViewModel = new ConsumeProviderViewModel(providerCatalog);

            ListProvidersView.DataContext = ProvidersViewModel;
            ConsumeProviderView.DataContext = ConsumeProviderViewModel;
            ProvidersViewModel.PropertyChanged += ProvidersViewModel_PropertyChanged;

            Closed += MainWindow_Closed;
        }

        public ProvidersViewModel ProvidersViewModel { get; }

        public ConsumeProviderViewModel ConsumeProviderViewModel { get; }

        private async void Root_Loaded(object sender, RoutedEventArgs e)
        {
            Root.Loaded -= Root_Loaded;

            try
            {
                await ProvidersViewModel.LoadProvidersAsync(_loadCancellation.Token);
                await ConsumeProviderViewModel.LoadProvidersAsync(_loadCancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void Root_SelectionChanged(
            NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            string? tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
            ListProvidersView.Visibility = tag == "ListProviders" ? Visibility.Visible : Visibility.Collapsed;
            ConsumeProviderView.Visibility = tag == "ConsumeProvider" ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            EtwProviderInfo? previousProvider = ProvidersViewModel.SelectedProvider;
            ProvidersViewModel.SearchText = ((TextBox)sender).Text;

            if (ProvidersViewModel.SelectedProvider is not null &&
                ProvidersViewModel.SelectedProvider != previousProvider)
            {
                try
                {
                    await LoadSelectedProviderSchemaAsync();
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private async void ProviderSearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;
            if (ProvidersViewModel.SelectedProvider is null)
            {
                return;
            }

            try
            {
                await LoadSelectedProviderSchemaAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async void ProvidersViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ProvidersViewModel.SelectedProvider))
            {
                return;
            }

            try
            {
                await LoadSelectedProviderSchemaAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void SchemaSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ProvidersViewModel.SelectedProviderDetails is not null)
            {
                ProvidersViewModel.SelectedProviderDetails.SchemaSearchText = ((TextBox)sender).Text;
            }
        }

        private void SchemaSearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
            }
        }

        private async void ProvidersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                await LoadSelectedProviderSchemaAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ConsumeSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ConsumeProviderViewModel.SearchText = ((TextBox)sender).Text;
            UpdateConsumeProviderMatchesVisibility();
        }

        private void ConsumeSearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            e.Handled = true;
            ConsumeProviderViewModel.SelectFirstMatchingProvider();
            UpdateConsumeProviderSearchText();
            UpdateConsumeProviderMatchesVisibility();
        }

        private void ConsumeProvidersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConsumeProviderSearchText();
            UpdateConsumeProviderMatchesVisibility();
        }

        private async void StartStopConsumingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ConsumeProviderViewModel.CanStop)
                {
                    await ConsumeProviderViewModel.StopAsync();
                }
                else
                {
                    await ConsumeProviderViewModel.StartAsync(_loadCancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void EventsPerPageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(((TextBox)sender).Text, out int eventsPerPage))
            {
                ConsumeProviderViewModel.EventsPerPage = eventsPerPage;
            }
        }

        private void PreviousEventsPageButton_Click(object sender, RoutedEventArgs e)
        {
            ConsumeProviderViewModel.GoToPreviousPage();
        }

        private void NextEventsPageButton_Click(object sender, RoutedEventArgs e)
        {
            ConsumeProviderViewModel.GoToNextPage();
        }

        private async void ExportEventsButton_Click(object sender, RoutedEventArgs e)
        {
            string format = ConsumeProviderViewModel.SelectedExportFormat;
            var picker = new FileSavePicker
            {
                SuggestedFileName = $"etw-events-{DateTimeOffset.Now:yyyyMMdd-HHmmss}",
            };

            if (format == "CSV")
            {
                picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
            }
            else
            {
                picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            }

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            try
            {
                string content = ConsumeProviderViewModel.CreateExportContent();
                if (string.IsNullOrEmpty(content))
                {
                    return;
                }

                await Windows.Storage.FileIO.WriteTextAsync(file, content);
                ConsumeProviderViewModel.ReportExported();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ConsumeProviderViewModel.ReportError(ex.Message);
            }
        }

        private void UpdateConsumeProviderSearchText()
        {
            string? selectedName = ConsumeProviderViewModel.SelectedProvider?.Name;
            if (string.IsNullOrWhiteSpace(selectedName) || ConsumeProviderSearchTextBox.Text == selectedName)
            {
                return;
            }

            ConsumeProviderSearchTextBox.Text = selectedName;
            ConsumeProviderSearchTextBox.SelectionStart = ConsumeProviderSearchTextBox.Text.Length;
        }

        private void UpdateConsumeProviderMatchesVisibility()
        {
            string searchText = ConsumeProviderSearchTextBox.Text.Trim();
            string? selectedName = ConsumeProviderViewModel.SelectedProvider?.Name;
            bool hasOpenSearch = !string.IsNullOrWhiteSpace(searchText) &&
                !string.Equals(searchText, selectedName, StringComparison.CurrentCultureIgnoreCase);

            ConsumeProviderMatchesListView.Visibility = hasOpenSearch
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            ProvidersViewModel.PropertyChanged -= ProvidersViewModel_PropertyChanged;
            _loadCancellation.Cancel();
            _schemaLoadCancellation?.Cancel();
            _schemaLoadCancellation?.Dispose();
            _loadCancellation.Dispose();
            await ConsumeProviderViewModel.DisposeAsync();
        }

        private async Task LoadSelectedProviderSchemaAsync()
        {
            _schemaLoadCancellation?.Cancel();
            _schemaLoadCancellation?.Dispose();
            _schemaLoadCancellation = CancellationTokenSource.CreateLinkedTokenSource(_loadCancellation.Token);

            await ProvidersViewModel.LoadSelectedProviderSchemaAsync(_schemaLoadCancellation.Token);
        }
    }
}

using System;
using Bear_Browser.Models;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Controls
{
    public sealed partial class HistoryView : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public HistoryViewModel ViewModel { get; }

        public HistoryView()
        {
            InitializeComponent();
            ViewModel = App.Services.GetRequiredService<HistoryViewModel>();

            FiltersListView.ItemsSource = ViewModel.Filters;
            FiltersListView.SelectedItem = ViewModel.SelectedFilter;
            HistoryListView.ItemsSource = ViewModel.DisplayedHistory;

            ViewModel.DisplayedHistory.CollectionChanged += (s, e) => UpdateEmptyState();
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool isEmpty = ViewModel.DisplayedHistory.Count == 0;
            EmptyStateContainer.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            HistoryListView.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            CountBadgeText.Text = $"{ViewModel.DisplayedHistory.Count} kayıt";
        }

        private void FiltersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltersListView.SelectedItem is string filter)
            {
                ViewModel.SelectedFilter = filter;
                UpdateEmptyState();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
            UpdateEmptyState();
        }

        private void HistoryListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is HistoryItem item)
            {
                NavigateRequested?.Invoke(this, item.Url);
            }
        }

        private async void DeleteHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                await ViewModel.RemoveHistoryItemAsync(id);
                UpdateEmptyState();
            }
        }

        private async void ClearHistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Tüm Geçmişi Temizle",
                Content = "Tüm gözatma geçmişiniz kalıcı olarak silinecek. Onaylıyor musunuz?",
                PrimaryButtonText = "Evet, Temizle",
                CloseButtonText = "İptal",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.ClearHistoryAsync();
                UpdateEmptyState();
            }
        }
    }
}
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
        }

        private void FiltersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltersListView.SelectedItem is string filter)
            {
                ViewModel.SelectedFilter = filter;
                CurrentFilterTitleText.Text = filter;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
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
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Controls
{
    public sealed partial class HistoryView : UserControl
    {
        public event EventHandler<string>? NavigateRequested;

        private readonly List<string> _filters = new() { "Tüm Geçmiþ", "Bugün", "Dün", "Daha Eski" };
        private string _selectedFilter = "Tüm Geçmiþ";

        public HistoryView()
        {
            InitializeComponent();
            FiltersListView.ItemsSource = _filters;
            FiltersListView.SelectedItem = _selectedFilter;
            RefreshList();

            ProfileService.Instance.HistoryUpdated += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(RefreshList);
            };
        }

        private void FiltersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FiltersListView.SelectedItem is string filter)
            {
                _selectedFilter = filter;
                CurrentFilterTitleText.Text = filter;
                RefreshList();
            }
        }

        private void RefreshList()
        {
            string query = SearchBox.Text?.Trim() ?? "";
            var items = ProfileService.Instance.History.AsEnumerable();

            // Tarih filtreleri
            DateTime today = DateTime.Today;
            if (_selectedFilter == "Bugün")
            {
                items = items.Where(h => h.VisitedAt.Date == today);
            }
            else if (_selectedFilter == "Dün")
            {
                items = items.Where(h => h.VisitedAt.Date == today.AddDays(-1));
            }
            else if (_selectedFilter == "Daha Eski")
            {
                items = items.Where(h => h.VisitedAt.Date < today.AddDays(-1));
            }

            // Metin arama
            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(h => h.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                         h.Url.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            HistoryListView.ItemsSource = items.ToList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshList();
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
                await ProfileService.Instance.RemoveHistoryItemAsync(id);
                RefreshList();
            }
        }

        private async void ClearHistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Tüm Geçmiþi Temizle",
                Content = "Tüm gözatma geçmiþiniz kalýcý olarak silinecek. Onaylýyor musunuz?",
                PrimaryButtonText = "Evet, Temizle",
                CloseButtonText = "Ýptal",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ProfileService.Instance.ClearHistoryAsync();
                RefreshList();
            }
        }
    }
}
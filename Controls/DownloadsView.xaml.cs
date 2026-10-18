using System;
using System.Diagnostics;
using System.IO;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Controls
{
    public sealed partial class DownloadsView : UserControl
    {
        public DownloadsViewModel ViewModel { get; }

        public DownloadsView()
        {
            InitializeComponent();
            ViewModel = App.Services.GetRequiredService<DownloadsViewModel>();
            DownloadsListView.ItemsSource = ViewModel.DisplayedDownloads;

            ViewModel.DisplayedDownloads.CollectionChanged += (s, e) => UpdateEmptyState();
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool isEmpty = ViewModel.DisplayedDownloads.Count == 0;
            EmptyStateContainer.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            DownloadsListView.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            if (CountBadgeText != null)
            {
                CountBadgeText.Text = $"{ViewModel.DisplayedDownloads.Count} dosya";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
            UpdateEmptyState();
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Process.Start("explorer.exe", downloadsDir);
            }
            catch { }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string path && File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            catch { }
        }

        private void ShowInFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is string path && File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    OpenFolderBtn_Click(sender, e);
                }
            }
            catch { }
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                await ViewModel.RemoveDownloadAsync(id);
                UpdateEmptyState();
            }
        }

        private async void ClearAllDownloads_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "İndirme Listesini Temizle",
                Content = "İndirme geçmişi temizlenecek (bilgisayarınızdaki gerçek dosyalar silinmez). Onaylıyor musunuz?",
                PrimaryButtonText = "Evet, Temizle",
                CloseButtonText = "İptal",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.ClearDownloadsAsync();
                UpdateEmptyState();
            }
        }
    }
}
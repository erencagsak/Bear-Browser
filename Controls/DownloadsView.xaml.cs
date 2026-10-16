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
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
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
                // Kullanıcı çalıştırmayı iptal etti veya onay vermedi
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
            }
        }

        private async void ClearAllDownloads_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ClearDownloadsAsync();
        }
    }
}
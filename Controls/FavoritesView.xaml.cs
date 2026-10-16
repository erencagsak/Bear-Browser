using System;
using System.Collections.Generic;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Bear_Browser.Controls
{
    public class FavoriteDisplayItem
    {
        public bool IsFolder { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string Glyph { get; set; } = "\uE774";
        public string Url { get; set; } = string.Empty;
        public object Tag { get; set; } = string.Empty;
        public string FaviconUrl { get; set; } = string.Empty;
        
        public Uri? FaviconUri
        {
            get
            {
                if (IsFolder || string.IsNullOrWhiteSpace(Url)) return null;
                try
                {
                    var uri = new Uri(Url);
                    if (!string.IsNullOrEmpty(uri.Host))
                        return new Uri($"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32");
                }
                catch { }
                return new Uri("ms-appx:///Assets/Bear-Browser-Logo.jpg");
            }
        }

        public Visibility FaviconVisibility => IsFolder ? Visibility.Collapsed : Visibility.Visible;
        public Visibility FolderVisibility => IsFolder ? Visibility.Visible : Visibility.Collapsed;
    }

    public sealed partial class FavoritesView : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public event EventHandler<string>? NewTabRequested;
        public event EventHandler? ToggleFavoritesBarRequested;

        private string _currentFolder = "Sık kullanılanlar çubuğu";

        public FavoritesView()
        {
            InitializeComponent();
            LoadData();

            ProfileService.Instance.BookmarksUpdated += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    FoldersListView.ItemsSource = null;
                    FoldersListView.ItemsSource = ProfileService.Instance.Folders;
                    RefreshCurrentList();
                });
            };
        }

        private void LoadData()
        {
            FoldersListView.ItemsSource = ProfileService.Instance.Folders;
            FoldersListView.SelectedItem = _currentFolder;
            RefreshCurrentList();
        }

        private void FoldersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoldersListView.SelectedItem is string folder)
            {
                _currentFolder = folder;
                RefreshCurrentList();
            }
        }

        private void RefreshCurrentList()
        {
            bool isRoot = _currentFolder == "Sık kullanılanlar çubuğu";
            BackButton.Visibility = isRoot ? Visibility.Collapsed : Visibility.Visible;
            BreadcrumbText.Text = isRoot ? "Sık Kullanılanlar" : $"Sık Kullanılanlar > {_currentFolder}";

            string query = SearchBox.Text?.Trim() ?? "";
            var displayList = new List<FavoriteDisplayItem>();

            // 1. Eğer Ana Kök Dizinindeysek, kullanıcının oluşturduğu özel KLASÖRLERİ de en üstte göster
            if (isRoot && string.IsNullOrEmpty(query))
            {
                var customFolders = ProfileService.Instance.Folders
                    .Where(f => !f.Equals("Sık kullanılanlar çubuğu", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var folder in customFolders)
                {
                    int count = ProfileService.Instance.Bookmarks.Count(b => b.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase));
                    displayList.Add(new FavoriteDisplayItem
                    {
                        IsFolder = true,
                        Title = folder,
                        Subtitle = $"{count} bağlantı (Klasör)",
                        Glyph = "\uED25",
                        Tag = folder
                    });
                }
            }

            // 2. Bu klasöre ait web sitelerini listele
            var bookmarks = ProfileService.Instance.Bookmarks
                .Where(b => b.Folder.Equals(_currentFolder, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrEmpty(query))
            {
                bookmarks = ProfileService.Instance.Bookmarks
                    .Where(b => b.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                b.Url.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var b in bookmarks)
            {
                displayList.Add(new FavoriteDisplayItem
                {
                    IsFolder = false,
                    Title = b.Title,
                    Subtitle = b.Url,
                    Glyph = "\uE774",
                    Url = b.Url,
                    Tag = b.Id,
                    FaviconUrl = b.FaviconUrl
                });
            }

            ItemsListView.ItemsSource = displayList;
        }

        private void ItemsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FavoriteDisplayItem item)
            {
                if (item.IsFolder)
                {
                    // Klasörün içine gir
                    _currentFolder = item.Title;
                    FoldersListView.SelectedItem = item.Title;
                    RefreshCurrentList();
                }
                else
                {
                    // Sayfaya git
                    NavigateRequested?.Invoke(this, item.Url);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _currentFolder = "Sık kullanılanlar çubuğu";
            FoldersListView.SelectedItem = _currentFolder;
            RefreshCurrentList();
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag is Guid id)
                {
                    await ProfileService.Instance.RemoveBookmarkAsync(id);
                }
                else if (btn.Tag is string folderName)
                {
                    await ProfileService.Instance.RemoveFolderAsync(folderName);
                }
                RefreshCurrentList();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshCurrentList();
        }

        private void ToggleBarBtn_Click(object sender, RoutedEventArgs e)
        {
            ToggleFavoritesBarRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void AddFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var textBox = new TextBox { PlaceholderText = "Klasör adı girin" };
            var dialog = new ContentDialog
            {
                Title = "Yeni Klasör Ekle",
                Content = textBox,
                PrimaryButtonText = "Ekle",
                CloseButtonText = "İptal",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                await ProfileService.Instance.AddFolderAsync(textBox.Text.Trim());
                RefreshCurrentList();
            }
        }

        private async void AddFavBtn_Click(object sender, RoutedEventArgs e)
        {
            var titleBox = new TextBox { PlaceholderText = "Başlık" };
            var urlBox = new TextBox { PlaceholderText = "https://..." };

            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(titleBox);
            panel.Children.Add(urlBox);

            var dialog = new ContentDialog
            {
                Title = "Sık Kullanılan Ekle",
                Content = panel,
                PrimaryButtonText = "Ekle",
                CloseButtonText = "İptal",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(urlBox.Text))
            {
                string t = string.IsNullOrWhiteSpace(titleBox.Text) ? urlBox.Text : titleBox.Text;
                await ProfileService.Instance.AddOrUpdateBookmarkAsync(urlBox.Text.Trim(), t, _currentFolder);
                RefreshCurrentList();
            }
        }

        private void ItemGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(sender as UIElement).Properties;
            if (props.IsMiddleButtonPressed)
            {
                e.Handled = true;
                if (sender is FrameworkElement fe && fe.DataContext is FavoriteDisplayItem item && !item.IsFolder && !string.IsNullOrEmpty(item.Url))
                {
                    NewTabRequested?.Invoke(this, item.Url);
                }
            }
        }

        private async void ImportBookmarksBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
                picker.FileTypeFilter.Add(".html");
                picker.FileTypeFilter.Add(".htm");
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    int count = await ProfileService.Instance.ImportBookmarksHtmlAsync(file.Path);
                    var dialog = new ContentDialog
                    {
                        Title = "Sık Kullanılanlar İçe Aktarıldı",
                        Content = count > 0
                            ? $"{count} adet sık kullanılan bağlantı başarıyla içe aktarıldı."
                            : "Dosyada yeni veya geçerli sık kullanılan bağlantı bulunamadı.",
                        CloseButtonText = "Tamam",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = "İçe Aktarma Hatası",
                    Content = $"Dosya okunurken bir hata oluştu: {ex.Message}",
                    CloseButtonText = "Tamam",
                    XamlRoot = this.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }

        private async void ExportBookmarksBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("HTML Dosyası (*.html)", new List<string> { ".html" });
                picker.SuggestedFileName = $"bear_bookmarks_{DateTime.Now:yyyyMMdd}";
                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await ProfileService.Instance.ExportBookmarksHtmlAsync(file.Path);
                    var dialog = new ContentDialog
                    {
                        Title = "Sık Kullanılanlar Dışa Aktarıldı",
                        Content = $"Tüm sık kullanılanlar başarıyla kaydedildi:\n{file.Path}",
                        CloseButtonText = "Tamam",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errDialog = new ContentDialog
                {
                    Title = "Dışa Aktarma Hatası",
                    Content = $"Dosya kaydedilirken bir hata oluştu: {ex.Message}",
                    CloseButtonText = "Tamam",
                    XamlRoot = this.XamlRoot
                };
                await errDialog.ShowAsync();
            }
        }
    }
}
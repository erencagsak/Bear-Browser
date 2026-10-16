using System;
using System.Collections.Generic;
using Bear_Browser.Models;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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

        public FavoritesViewModel ViewModel { get; }

        public FavoritesView()
        {
            InitializeComponent();
            ViewModel = App.Services.GetRequiredService<FavoritesViewModel>();

            FoldersListView.ItemsSource = ViewModel.Folders;
            FoldersListView.SelectedItem = ViewModel.CurrentFolder;
            ItemsListView.ItemsSource = ViewModel.DisplayedItems;

            UpdateNavigationUI();
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.CurrentFolder) ||
                    e.PropertyName == nameof(ViewModel.IsRootFolder) ||
                    e.PropertyName == nameof(ViewModel.BreadcrumbText))
                {
                    UpdateNavigationUI();
                }
            };
        }

        private void UpdateNavigationUI()
        {
            BackButton.Visibility = ViewModel.IsRootFolder ? Visibility.Collapsed : Visibility.Visible;
            BreadcrumbText.Text = ViewModel.BreadcrumbText;
        }

        private void FoldersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoldersListView.SelectedItem is string folder)
            {
                ViewModel.CurrentFolder = folder;
            }
        }

        private void ItemsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FavoriteDisplayItem item)
            {
                if (item.IsFolder)
                {
                    ViewModel.OpenFolder(item.Title);
                    FoldersListView.SelectedItem = item.Title;
                }
                else
                {
                    NavigateRequested?.Invoke(this, item.Url);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigateToRoot();
            FoldersListView.SelectedItem = ViewModel.CurrentFolder;
        }

        private async void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Tag is Guid id)
                {
                    await ViewModel.RemoveBookmarkAsync(id);
                }
                else if (btn.Tag is string folderName)
                {
                    await ViewModel.RemoveFolderAsync(folderName);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
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
                await ViewModel.AddFolderAsync(textBox.Text.Trim());
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
                await ViewModel.AddOrUpdateBookmarkAsync(urlBox.Text.Trim(), t, ViewModel.CurrentFolder);
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
                    int count = await ViewModel.ImportBookmarksHtmlAsync(file.Path);
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
                    await ViewModel.ExportBookmarksHtmlAsync(file.Path);
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
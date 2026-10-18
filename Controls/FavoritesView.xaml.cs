using System;
using System.Collections.Generic;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

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
                if (IsFolder) return null;
                if (!string.IsNullOrWhiteSpace(FaviconUrl))
                {
                    try { return new Uri(FaviconUrl); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(Url))
                {
                    try
                    {
                        var uri = new Uri(Url);
                        if (!string.IsNullOrEmpty(uri.Host))
                            return new Uri($"https://icons.duckduckgo.com/ip3/{uri.Host}.ico");
                    }
                    catch { }
                }
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

        private readonly IBookmarkService _bookmarkService;
        public FavoritesViewModel ViewModel { get; }

        public FavoritesView()
        {
            InitializeComponent();
            _bookmarkService = App.Services.GetRequiredService<IBookmarkService>();
            ViewModel = App.Services.GetRequiredService<FavoritesViewModel>();

            ItemsListView.ItemsSource = ViewModel.DisplayedItems;

            UpdateNavigationUI();
            ViewModel.DisplayedItems.CollectionChanged += (s, e) =>
            {
                CountBadgeText.Text = $"{ViewModel.DisplayedItems.Count} yer imi";
            };

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
            CountBadgeText.Text = $"{ViewModel.DisplayedItems.Count} yer imi";
        }

        private void ItemsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FavoriteDisplayItem item)
            {
                if (item.IsFolder)
                {
                    ViewModel.OpenFolder(item.Title);
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

        private void BreadcrumbArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }

        private async void BreadcrumbArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string data = await e.DataView.GetTextAsync();
                    if (data.StartsWith("bear-bookmark-id:"))
                    {
                        string idStr = data.Substring("bear-bookmark-id:".Length);
                        if (Guid.TryParse(idStr, out Guid bId))
                        {
                            var item = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == bId);
                            if (item != null)
                            {
                                item.Folder = "Sık kullanılanlar çubuğu";
                                _bookmarkService.Bookmarks.Remove(item);
                                _bookmarkService.Bookmarks.Add(item);
                                await _bookmarkService.SaveChangesAsync();
                                ViewModel.RefreshList();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void ItemCard_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is FavoriteDisplayItem displayItem)
            {
                if (displayItem.IsFolder)
                {
                    e.Data.SetText("bear-folder:" + displayItem.Title);
                }
                else if (displayItem.Tag is Guid bId)
                {
                    e.Data.SetText("bear-bookmark-id:" + bId.ToString());
                }
                e.Data.RequestedOperation = DataPackageOperation.Move;
            }
        }

        private void ItemCard_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            if (sender is Border border)
            {
                var p = e.GetPosition(border);
                if (border.Tag is FavoriteDisplayItem displayItem && displayItem.IsFolder)
                {
                    border.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 249, 115, 22));
                    border.BorderThickness = new Thickness(2);
                }
                else
                {
                    border.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 249, 115, 22));
                    if (p.Y < border.ActualHeight / 2.0)
                    {
                        border.BorderThickness = new Thickness(0, 2.5, 0, 0);
                    }
                    else
                    {
                        border.BorderThickness = new Thickness(0, 0, 0, 2.5);
                    }
                }
            }
        }

        private void ItemCard_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)Application.Current.Resources["ArcBorderSubtleBrush"];
                border.BorderThickness = new Thickness(1);
            }
        }

        private async void ItemCard_Drop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not FavoriteDisplayItem targetItem)
                return;

            if (sender is Border border)
            {
                border.BorderBrush = (Brush)Application.Current.Resources["ArcBorderSubtleBrush"];
                border.BorderThickness = new Thickness(1);
            }

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string data = await e.DataView.GetTextAsync();

                    if (data.StartsWith("bear-bookmark-id:"))
                    {
                        string idStr = data.Substring("bear-bookmark-id:".Length);
                        if (Guid.TryParse(idStr, out Guid srcId))
                        {
                            var srcItem = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == srcId);
                            if (srcItem != null)
                            {
                                if (targetItem.IsFolder)
                                {
                                    srcItem.Folder = targetItem.Title;
                                    _bookmarkService.Bookmarks.Remove(srcItem);
                                    _bookmarkService.Bookmarks.Add(srcItem);
                                    await _bookmarkService.SaveChangesAsync();
                                    ViewModel.RefreshList();
                                }
                                else if (targetItem.Tag is Guid targetGuid && targetGuid != srcId)
                                {
                                    var targetBookmark = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == targetGuid);
                                    if (targetBookmark != null)
                                    {
                                        var p = e.GetPosition(fe);
                                        bool insertBefore = p.Y < (fe.ActualHeight / 2.0);

                                        srcItem.Folder = targetBookmark.Folder;
                                        _bookmarkService.Bookmarks.Remove(srcItem);

                                        int targetIndex = _bookmarkService.Bookmarks.IndexOf(targetBookmark);
                                        if (targetIndex >= 0)
                                        {
                                            if (!insertBefore) targetIndex++;
                                            if (targetIndex <= _bookmarkService.Bookmarks.Count)
                                                _bookmarkService.Bookmarks.Insert(targetIndex, srcItem);
                                            else
                                                _bookmarkService.Bookmarks.Add(srcItem);
                                        }
                                        else
                                        {
                                            _bookmarkService.Bookmarks.Add(srcItem);
                                        }

                                        await _bookmarkService.SaveChangesAsync();
                                        ViewModel.RefreshList();
                                    }
                                }
                            }
                        }
                    }
                    else if (data.StartsWith("bear-folder:"))
                    {
                        string srcFolderName = data.Substring("bear-folder:".Length);
                        if (targetItem.IsFolder && !srcFolderName.Equals(targetItem.Title, StringComparison.OrdinalIgnoreCase))
                        {
                            var p = e.GetPosition(fe);
                            bool insertBefore = p.Y < (fe.ActualHeight / 2.0);

                            int srcIndex = _bookmarkService.Folders.IndexOf(srcFolderName);
                            if (srcIndex >= 0)
                            {
                                _bookmarkService.Folders.RemoveAt(srcIndex);
                                int targetIndex = _bookmarkService.Folders.IndexOf(targetItem.Title);
                                if (targetIndex >= 0)
                                {
                                    if (!insertBefore) targetIndex++;
                                    if (targetIndex <= _bookmarkService.Folders.Count)
                                        _bookmarkService.Folders.Insert(targetIndex, srcFolderName);
                                    else
                                        _bookmarkService.Folders.Add(srcFolderName);
                                }
                                else
                                {
                                    _bookmarkService.Folders.Add(srcFolderName);
                                }

                                await _bookmarkService.SaveChangesAsync();
                                ViewModel.SyncFolders();
                                ViewModel.RefreshList();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void ItemsListView_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }

        private async void ItemsListView_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string data = await e.DataView.GetTextAsync();
                    if (data.StartsWith("bear-bookmark-id:"))
                    {
                        string idStr = data.Substring("bear-bookmark-id:".Length);
                        if (Guid.TryParse(idStr, out Guid srcId))
                        {
                            var srcItem = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == srcId);
                            if (srcItem != null)
                            {
                                srcItem.Folder = ViewModel.CurrentFolder;
                                _bookmarkService.Bookmarks.Remove(srcItem);
                                _bookmarkService.Bookmarks.Add(srcItem);
                                await _bookmarkService.SaveChangesAsync();
                                ViewModel.RefreshList();
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Bear_Browser.Controls
{
    public sealed partial class FavoritesBar : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public event EventHandler<string>? NewTabRequested;
        public event EventHandler<string>? NewWindowRequested;
        public event EventHandler<string>? NewInPrivateRequested;
        public event EventHandler? HideFavoritesBarRequested;

        private readonly IBookmarkService _bookmarkService;
        private BookmarkItem? _editingBookmark;

        private class BarItemModel
        {
            public bool IsFolder { get; set; }
            public string FolderName { get; set; } = string.Empty;
            public List<BookmarkItem> FolderBookmarks { get; set; } = new();
            public BookmarkItem? Bookmark { get; set; }
            public FrameworkElement BarElement { get; set; } = null!;
            public double MeasuredWidth { get; set; }
        }

        private readonly List<BarItemModel> _itemModels = new();
        private bool _isAnyFlyoutOpen = false;
        private bool _needsRefreshAfterFlyoutClose = false;
        private Flyout? _currentOpenFolderFlyout = null;

        public FavoritesBar()
        {
            InitializeComponent();
            _bookmarkService = App.Services.GetRequiredService<IBookmarkService>();
            _bookmarkService.BookmarksUpdated += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(RefreshBar);
            };
            RefreshBar();
        }

        public void RefreshBar()
        {
            if (_isAnyFlyoutOpen)
            {
                _needsRefreshAfterFlyoutClose = true;
                return;
            }

            _itemModels.Clear();
            _bookmarkService.EnsureBarOrder();

            foreach (var orderKey in _bookmarkService.BarOrder)
            {
                if (orderKey.StartsWith("folder:"))
                {
                    string folder = orderKey.Substring("folder:".Length);
                    if (!_bookmarkService.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var folderBookmarks = _bookmarkService.Bookmarks
                        .Where(b => b.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var dropBtn = CreateFolderButton(folder, folderBookmarks);

                    double folderTextEst = Math.Max(20.0, folder.Length * 7.5);
                    double w = 12 + 6 + folderTextEst + 16 + 18;
                    if (dropBtn is Border dropBorder && dropBorder.Child is StackPanel spFolder)
                    {
                        spFolder.Measure(new Windows.Foundation.Size(double.PositiveInfinity, 28));
                        if (spFolder.DesiredSize.Width > 20)
                        {
                            w = spFolder.DesiredSize.Width + 16 + 18;
                        }
                    }

                    _itemModels.Add(new BarItemModel
                    {
                        IsFolder = true,
                        FolderName = folder,
                        FolderBookmarks = folderBookmarks,
                        BarElement = dropBtn,
                        MeasuredWidth = w
                    });
                }
                else if (orderKey.StartsWith("bm:"))
                {
                    string idStr = orderKey.Substring("bm:".Length);
                    if (!Guid.TryParse(idStr, out Guid bId)) continue;

                    var item = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == bId && b.Folder.Equals("Sık kullanılanlar çubuğu", StringComparison.OrdinalIgnoreCase));
                    if (item == null) continue;

                    var btn = CreateBookmarkButton(item);

                    double textEst = Math.Min(130.0, Math.Max(24.0, item.Title.Length * 7.5));
                    double w = 14 + 6 + textEst + 16;
                    if (btn is Border bBorder && bBorder.Child is StackPanel sp)
                    {
                        sp.Measure(new Windows.Foundation.Size(double.PositiveInfinity, 28));
                        if (sp.DesiredSize.Width > 20)
                        {
                            w = sp.DesiredSize.Width + 16;
                        }
                    }

                    _itemModels.Add(new BarItemModel
                    {
                        IsFolder = false,
                        Bookmark = item,
                        BarElement = btn,
                        MeasuredWidth = w
                    });
                }
            }

            LayoutBarItems();
        }

        private void LayoutBarItems(double? overrideWidth = null)
        {
            BarItemsPanel.Children.Clear();
            OverflowFlyout.Items.Clear();

            if (_itemModels.Count == 0)
            {
                OverflowButton.Visibility = Visibility.Collapsed;
                return;
            }

            double widthToUse = overrideWidth ?? BarRootGrid.ActualWidth;
            double totalAvailable = widthToUse - BarRootGrid.Padding.Left - BarRootGrid.Padding.Right;
            if (totalAvailable <= 0)
            {
                foreach (var model in _itemModels)
                {
                    BarItemsPanel.Children.Add(model.BarElement);
                }
                OverflowButton.Visibility = Visibility.Collapsed;
                return;
            }

            const double spacing = 4.0;
            const double overflowBtnWidth = 32.0;

            double totalRequired = 0;
            for (int i = 0; i < _itemModels.Count; i++)
            {
                totalRequired += _itemModels[i].MeasuredWidth;
                if (i > 0) totalRequired += spacing;
            }

            if (totalRequired <= totalAvailable)
            {
                OverflowButton.Visibility = Visibility.Collapsed;
                foreach (var model in _itemModels)
                {
                    BarItemsPanel.Children.Add(model.BarElement);
                }
            }
            else
            {
                OverflowButton.Visibility = Visibility.Visible;
                double availableForVisible = Math.Max(0, totalAvailable - overflowBtnWidth);
                double used = 0;
                int visibleCount = 0;

                for (int i = 0; i < _itemModels.Count; i++)
                {
                    var model = _itemModels[i];
                    double needed = (visibleCount == 0) ? model.MeasuredWidth : (model.MeasuredWidth + spacing);

                    if (used + needed <= availableForVisible)
                    {
                        used += needed;
                        visibleCount++;
                        BarItemsPanel.Children.Add(model.BarElement);
                    }
                    else
                    {
                        AddOverflowItem(model);
                    }
                }
            }
        }

        private void BarRootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                LayoutBarItems(e.NewSize.Width);
            }
        }

        private void AddOverflowItem(BarItemModel model)
        {
            if (model.IsFolder)
            {
                string displayFolderName = model.FolderName;
                if (!string.IsNullOrWhiteSpace(displayFolderName) && displayFolderName.Length > 20)
                {
                    displayFolderName = displayFolderName.Substring(0, 18).TrimEnd() + "…";
                }

                var subItem = new MenuFlyoutSubItem
                {
                    Text = displayFolderName,
                    Icon = new FontIcon { Glyph = "\uED25", FontSize = 12 },
                    MaxWidth = 200,
                    Height = 32
                };
                ToolTipService.SetToolTip(subItem, model.FolderName);

                if (model.FolderBookmarks.Count == 0)
                {
                    subItem.Items.Add(new MenuFlyoutItem { Text = "(Klasör boş)", IsEnabled = false, Height = 30 });
                }
                else
                {
                    foreach (var b in model.FolderBookmarks)
                    {
                        var folderSubItem = CreateOverflowBookmarkItem(b);
                        subItem.Items.Add(folderSubItem);
                    }
                }

                OverflowFlyout.Items.Add(subItem);
            }
            else if (model.Bookmark != null)
            {
                var item = CreateOverflowBookmarkItem(model.Bookmark);
                OverflowFlyout.Items.Add(item);
            }
        }

        private MenuFlyoutItem CreateOverflowBookmarkItem(BookmarkItem b)
        {
            string displayTitle = b.Title;
            if (!string.IsNullOrWhiteSpace(displayTitle) && displayTitle.Length > 20)
            {
                displayTitle = displayTitle.Substring(0, 18).TrimEnd() + "…";
            }

            var item = new MenuFlyoutItem
            {
                Text = displayTitle,
                Tag = b,
                MaxWidth = 200,
                Height = 32,
                Padding = new Thickness(10, 4, 10, 4)
            };
            ToolTipService.SetToolTip(item, b.Title);

            string effectiveFavicon = b.FaviconUrl;
            if (string.IsNullOrWhiteSpace(effectiveFavicon) && !string.IsNullOrWhiteSpace(b.Url))
            {
                try
                {
                    var u = new Uri(b.Url);
                    if (!string.IsNullOrEmpty(u.Host))
                        effectiveFavicon = $"https://icons.duckduckgo.com/ip3/{u.Host}.ico";
                }
                catch { }
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(effectiveFavicon) && Uri.TryCreate(effectiveFavicon, UriKind.Absolute, out var uri))
                {
                    item.Icon = new ImageIcon
                    {
                        Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri)
                    };
                }
                else
                {
                    item.Icon = new FontIcon { Glyph = "\uE774", FontSize = 12 };
                }
            }
            catch
            {
                item.Icon = new FontIcon { Glyph = "\uE774", FontSize = 12 };
            }

            item.Click += (s, e) =>
            {
                NavigateRequested?.Invoke(this, b.Url);
            };

            item.PointerPressed += (s, e) =>
            {
                var props = e.GetCurrentPoint(item).Properties;
                if (props.IsMiddleButtonPressed)
                {
                    e.Handled = true;
                    NewTabRequested?.Invoke(this, b.Url);
                    OverflowFlyout.Hide();
                }
            };

            var itemContext = new MenuFlyout();
            var newTab = new MenuFlyoutItem { Text = "Yeni sekmede aç" };
            newTab.Click += (s, e) => { NewTabRequested?.Invoke(this, b.Url); OverflowFlyout.Hide(); };
            var newWin = new MenuFlyoutItem { Text = "Yeni pencerede aç" };
            newWin.Click += (s, e) => { NewWindowRequested?.Invoke(this, b.Url); OverflowFlyout.Hide(); };
            var newInPrivate = new MenuFlyoutItem { Text = "Yeni InPrivate penceresinde aç" };
            newInPrivate.Click += (s, e) => { NewInPrivateRequested?.Invoke(this, b.Url); OverflowFlyout.Hide(); };
            var editItem = new MenuFlyoutItem { Text = "Düzenle" };
            editItem.Click += (s, e) => { OverflowFlyout.Hide(); ShowEditBookmarkDialog(b); };
            var delItem = new MenuFlyoutItem { Text = "Sil" };
            delItem.Click += async (s, e) => { OverflowFlyout.Hide(); await _bookmarkService.RemoveBookmarkAsync(b.Id); };

            itemContext.Items.Add(newTab);
            itemContext.Items.Add(newWin);
            itemContext.Items.Add(newInPrivate);
            itemContext.Items.Add(new MenuFlyoutSeparator());
            itemContext.Items.Add(editItem);
            itemContext.Items.Add(delItem);

            item.ContextFlyout = itemContext;
            return item;
        }

        private FrameworkElement CreateFolderButton(string folder, List<BookmarkItem> folderBookmarks)
        {
            var border = new Border
            {
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Tag = folder,
                CanDrag = true,
                AllowDrop = true
            };

            var flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };
            var flyoutStyle = new Style(typeof(FlyoutPresenter));
            flyoutStyle.Setters.Add(new Setter(FlyoutPresenter.PaddingProperty, new Thickness(4)));
            flyoutStyle.Setters.Add(new Setter(FlyoutPresenter.MinWidthProperty, 180.0));
            flyoutStyle.Setters.Add(new Setter(FlyoutPresenter.MaxWidthProperty, 260.0));
            flyoutStyle.Setters.Add(new Setter(FlyoutPresenter.CornerRadiusProperty, new CornerRadius(10)));
            flyout.FlyoutPresenterStyle = flyoutStyle;

            bool isFlyoutOpen = false;
            border.PointerEntered += (s, e) =>
            {
                if (!isFlyoutOpen)
                    border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            };
            border.PointerExited += (s, e) =>
            {
                if (!isFlyoutOpen)
                    border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            };

            border.DragStarting += (s, e) =>
            {
                e.Data.SetText("bear-folder:" + folder);
                e.Data.RequestedOperation = DataPackageOperation.Move;
            };

            border.DragOver += (s, e) =>
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                HideDropIndicator();
                border.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(60, 249, 115, 22));
                border.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(180, 249, 115, 22));
                border.BorderThickness = new Thickness(1);

                if (!isFlyoutOpen)
                {
                    flyout.ShowAt(border);
                }
            };

            border.DragLeave += (s, e) =>
            {
                if (!isFlyoutOpen)
                {
                    border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    border.BorderThickness = new Thickness(0);
                }
            };

            var sp = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Spacing = 6, 
                VerticalAlignment = VerticalAlignment.Center 
            };
            sp.Children.Add(new FontIcon 
            { 
                Glyph = "\uED25", 
                FontSize = 12, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            sp.Children.Add(new TextBlock 
            { 
                Text = folder, 
                FontSize = 12, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            sp.Children.Add(new FontIcon 
            { 
                Glyph = "\uE70D", 
                FontSize = 9, 
                Opacity = 0.7, 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });
            border.Child = sp;

            var folderContext = new MenuFlyout();
            var openAllItem = new MenuFlyoutItem { Text = "Tümünü yeni sekmelerde aç" };
            openAllItem.Click += (s, e) =>
            {
                foreach (var b in folderBookmarks)
                {
                    NewTabRequested?.Invoke(this, b.Url);
                }
            };
            folderContext.Items.Add(openAllItem);

            var addFolderSubItem = new MenuFlyoutItem { Text = "Yeni klasör ekle" };
            addFolderSubItem.Click += (s, e) => ShowNewFolderDialog();
            folderContext.Items.Add(addFolderSubItem);

            folderContext.Items.Add(new MenuFlyoutSeparator());

            var deleteFolderItem = new MenuFlyoutItem { Text = "Klasörü sil" };
            deleteFolderItem.Click += async (s, e) =>
            {
                await _bookmarkService.RemoveFolderAsync(folder);
                RefreshBar();
            };
            folderContext.Items.Add(deleteFolderItem);
            border.ContextFlyout = folderContext;

            flyout.Opened += (s, e) =>
            {
                _isAnyFlyoutOpen = true;
                _currentOpenFolderFlyout = flyout;
                isFlyoutOpen = true;
                border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                border.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(160, 249, 115, 22));
                border.BorderThickness = new Thickness(1);
            };
            flyout.Closed += (s, e) =>
            {
                if (_currentOpenFolderFlyout == flyout)
                {
                    _currentOpenFolderFlyout = null;
                }
                _isAnyFlyoutOpen = false;
                isFlyoutOpen = false;
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                border.BorderThickness = new Thickness(0);

                if (_needsRefreshAfterFlyoutClose)
                {
                    _needsRefreshAfterFlyoutClose = false;
                    RefreshBar();
                }
            };

            var listPanel = new StackPanel 
            { 
                Spacing = 2, 
                AllowDrop = true, 
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) 
            };

            Border? emptyPlaceholder = null;

            Border CreateFolderRow(BookmarkItem b)
            {
                var row = new Border
                {
                    Height = 30,
                    Padding = new Thickness(8, 2, 8, 2),
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Tag = b,
                    CanDrag = true,
                    AllowDrop = true
                };

                bool isRowDragging = false;

                row.PointerEntered += (s, e) =>
                {
                    row.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(35, 255, 255, 255));
                };
                row.PointerExited += (s, e) =>
                {
                    row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                };

                row.Tapped += (s, e) =>
                {
                    if (isRowDragging) return;
                    NavigateRequested?.Invoke(this, b.Url);
                    flyout.Hide();
                };

                row.PointerPressed += (s, e) =>
                {
                    var props = e.GetCurrentPoint(row).Properties;
                    if (props.IsMiddleButtonPressed)
                    {
                        e.Handled = true;
                        NewTabRequested?.Invoke(this, b.Url);
                        flyout.Hide();
                    }
                };

                row.DragStarting += (s, e) =>
                {
                    isRowDragging = true;
                    e.Data.SetText("bear-bookmark-id:" + b.Id.ToString());
                    e.Data.RequestedOperation = DataPackageOperation.Move;
                };

                row.DropCompleted += (s, e) =>
                {
                    DispatcherQueue?.TryEnqueue(async () =>
                    {
                        await Task.Delay(150);
                        isRowDragging = false;
                    });
                };

                row.DragOver += (s, e) =>
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                    var p = e.GetPosition(row);
                    row.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 249, 115, 22));
                    if (p.Y < row.ActualHeight / 2.0)
                    {
                        row.BorderThickness = new Thickness(0, 2, 0, 0);
                    }
                    else
                    {
                        row.BorderThickness = new Thickness(0, 0, 0, 2);
                    }
                };

                row.DragLeave += (s, e) =>
                {
                    row.BorderThickness = new Thickness(0);
                };

                row.Drop += async (s, e) =>
                {
                    row.BorderThickness = new Thickness(0);
                    await HandleDropOnFolderRow(e, b, row);
                };

                var rowContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                var img = new Image { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
                string favUrl = b.FaviconUrl;
                if (string.IsNullOrWhiteSpace(favUrl) && !string.IsNullOrWhiteSpace(b.Url))
                {
                    try
                    {
                        var u = new Uri(b.Url);
                        if (!string.IsNullOrEmpty(u.Host)) favUrl = $"https://icons.duckduckgo.com/ip3/{u.Host}.ico";
                    }
                    catch { }
                }
                try
                {
                    if (!string.IsNullOrWhiteSpace(favUrl))
                        img.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(favUrl));
                }
                catch { }

                rowContent.Children.Add(img);
                rowContent.Children.Add(new TextBlock
                {
                    Text = b.Title,
                    FontSize = 12,
                    MaxWidth = 200,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Child = rowContent;

                var itemContext = new MenuFlyout();
                var newTab = new MenuFlyoutItem { Text = "Yeni sekmede aç" };
                newTab.Click += (s, e) => { NewTabRequested?.Invoke(this, b.Url); flyout.Hide(); };
                var newWin = new MenuFlyoutItem { Text = "Yeni pencerede aç" };
                newWin.Click += (s, e) => { NewWindowRequested?.Invoke(this, b.Url); flyout.Hide(); };
                var newInPrivate = new MenuFlyoutItem { Text = "Yeni InPrivate penceresinde aç" };
                newInPrivate.Click += (s, e) => { NewInPrivateRequested?.Invoke(this, b.Url); flyout.Hide(); };
                var editItem = new MenuFlyoutItem { Text = "Düzenle" };
                editItem.Click += (s, e) => { flyout.Hide(); ShowEditBookmarkDialog(b); };
                var delItem = new MenuFlyoutItem { Text = "Sil" };
                delItem.Click += async (s, e) => { flyout.Hide(); await _bookmarkService.RemoveBookmarkAsync(b.Id); };

                itemContext.Items.Add(newTab);
                itemContext.Items.Add(newWin);
                itemContext.Items.Add(newInPrivate);
                itemContext.Items.Add(new MenuFlyoutSeparator());
                itemContext.Items.Add(editItem);
                itemContext.Items.Add(delItem);
                row.ContextFlyout = itemContext;

                return row;
            }

            async Task HandleMoveBookmarkToFolder(BookmarkItem srcItem, BookmarkItem? targetItem, bool insertBefore)
            {
                bool wasAlreadyInThisFolder = srcItem.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase);

                srcItem.Folder = folder;
                _bookmarkService.BarOrder.Remove("bm:" + srcItem.Id.ToString());
                _bookmarkService.Bookmarks.Remove(srcItem);

                if (targetItem != null)
                {
                    int targetIndex = _bookmarkService.Bookmarks.IndexOf(targetItem);
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
                }
                else
                {
                    _bookmarkService.Bookmarks.Add(srcItem);
                }

                await _bookmarkService.SaveChangesAsync();

                if (emptyPlaceholder != null && listPanel.Children.Contains(emptyPlaceholder))
                {
                    listPanel.Children.Remove(emptyPlaceholder);
                }

                if (wasAlreadyInThisFolder)
                {
                    int srcUiIndex = -1;
                    int targetUiIndex = -1;
                    for (int i = 0; i < listPanel.Children.Count; i++)
                    {
                        if (listPanel.Children[i] is FrameworkElement fe && fe.Tag is BookmarkItem bi)
                        {
                            if (bi.Id == srcItem.Id) srcUiIndex = i;
                            if (targetItem != null && bi.Id == targetItem.Id) targetUiIndex = i;
                        }
                    }
                    if (srcUiIndex >= 0)
                    {
                        var rowEl = listPanel.Children[srcUiIndex];
                        listPanel.Children.RemoveAt(srcUiIndex);
                        if (targetUiIndex >= 0)
                        {
                            if (!insertBefore && targetUiIndex > srcUiIndex) targetUiIndex--;
                            if (!insertBefore) targetUiIndex++;
                            int clamped = Math.Clamp(targetUiIndex, 0, listPanel.Children.Count);
                            listPanel.Children.Insert(clamped, rowEl);
                        }
                        else
                        {
                            listPanel.Children.Add(rowEl);
                        }
                    }
                }
                else
                {
                    var newRow = CreateFolderRow(srcItem);
                    if (targetItem != null)
                    {
                        int targetUiIndex = -1;
                        for (int i = 0; i < listPanel.Children.Count; i++)
                        {
                            if (listPanel.Children[i] is FrameworkElement fe && fe.Tag is BookmarkItem bi && bi.Id == targetItem.Id)
                            {
                                targetUiIndex = i;
                                break;
                            }
                        }
                        if (targetUiIndex >= 0)
                        {
                            if (!insertBefore) targetUiIndex++;
                            int clamped = Math.Clamp(targetUiIndex, 0, listPanel.Children.Count);
                            listPanel.Children.Insert(clamped, newRow);
                        }
                        else
                        {
                            listPanel.Children.Add(newRow);
                        }
                    }
                    else
                    {
                        listPanel.Children.Add(newRow);
                    }

                    var barModel = _itemModels.FirstOrDefault(m => !m.IsFolder && m.Bookmark?.Id == srcItem.Id);
                    if (barModel != null)
                    {
                        _itemModels.Remove(barModel);
                        BarItemsPanel.Children.Remove(barModel.BarElement);
                    }
                }
            }

            async Task HandleDropOnFolderRow(DragEventArgs e, BookmarkItem targetBookmark, Border targetRow)
            {
                try
                {
                    if (!e.DataView.Contains(StandardDataFormats.Text)) return;
                    string data = await e.DataView.GetTextAsync();
                    if (!data.StartsWith("bear-bookmark-id:")) return;

                    string idStr = data.Substring("bear-bookmark-id:".Length);
                    if (!Guid.TryParse(idStr, out Guid srcId) || srcId == targetBookmark.Id) return;

                    var srcItem = _bookmarkService.Bookmarks.FirstOrDefault(x => x.Id == srcId);
                    if (srcItem == null) return;

                    var p = e.GetPosition(targetRow);
                    bool insertBefore = p.Y < (targetRow.ActualHeight / 2.0);

                    await HandleMoveBookmarkToFolder(srcItem, targetBookmark, insertBefore);
                }
                catch { }
            }

            async Task HandleDropOnFolderBackground(DragEventArgs e)
            {
                try
                {
                    if (!e.DataView.Contains(StandardDataFormats.Text)) return;
                    string data = await e.DataView.GetTextAsync();
                    if (!data.StartsWith("bear-bookmark-id:")) return;

                    string idStr = data.Substring("bear-bookmark-id:".Length);
                    if (!Guid.TryParse(idStr, out Guid srcId)) return;

                    var srcItem = _bookmarkService.Bookmarks.FirstOrDefault(x => x.Id == srcId);
                    if (srcItem == null) return;

                    await HandleMoveBookmarkToFolder(srcItem, null, false);
                }
                catch { }
            }

            border.Drop += async (s, e) =>
            {
                HideDropIndicator();
                if (!isFlyoutOpen)
                {
                    border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    border.BorderThickness = new Thickness(0);
                }

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
                                    await HandleMoveBookmarkToFolder(item, null, false);
                                }
                            }
                        }
                        else if (data.StartsWith("bear-folder:"))
                        {
                            string srcFolder = data.Substring("bear-folder:".Length);
                            if (!srcFolder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                            {
                                var p = e.GetPosition(border);
                                bool insertBefore = p.X < (border.ActualWidth / 2.0);

                                MoveBarOrderItem("folder:" + srcFolder, "folder:" + folder, insertBefore);
                                await _bookmarkService.SaveChangesAsync();
                                RefreshBar();
                            }
                        }
                    }
                }
                catch { }
            };

            if (folderBookmarks.Count == 0)
            {
                emptyPlaceholder = new Border
                {
                    Padding = new Thickness(14, 10, 14, 10),
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    AllowDrop = true
                };
                emptyPlaceholder.Child = new TextBlock
                {
                    Text = "(Klasör boş - buraya sürükleyin)",
                    FontSize = 12,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                emptyPlaceholder.DragOver += (s, e) =>
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                    emptyPlaceholder.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(40, 249, 115, 22));
                    emptyPlaceholder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(160, 249, 115, 22));
                    emptyPlaceholder.BorderThickness = new Thickness(1);
                };
                emptyPlaceholder.DragLeave += (s, e) =>
                {
                    emptyPlaceholder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    emptyPlaceholder.BorderThickness = new Thickness(0);
                };
                emptyPlaceholder.Drop += async (s, e) =>
                {
                    emptyPlaceholder.BorderThickness = new Thickness(0);
                    emptyPlaceholder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    await HandleDropOnFolderBackground(e);
                };
                listPanel.Children.Add(emptyPlaceholder);
            }
            else
            {
                foreach (var b in folderBookmarks)
                {
                    listPanel.Children.Add(CreateFolderRow(b));
                }
            }

            listPanel.DragOver += (s, e) =>
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            };
            listPanel.Drop += async (s, e) =>
            {
                await HandleDropOnFolderBackground(e);
            };

            flyout.Content = listPanel;

            border.Tapped += (s, e) =>
            {
                flyout.ShowAt(border);
            };

            return border;
        }

        private FrameworkElement CreateBookmarkButton(BookmarkItem item)
        {
            var border = new Border
            {
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Tag = item,
                CanDrag = true,
                AllowDrop = true
            };

            border.PointerEntered += (s, e) =>
            {
                border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            };
            border.PointerExited += (s, e) =>
            {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            };

            border.DragStarting += (s, e) =>
            {
                e.Data.SetText("bear-bookmark-id:" + item.Id.ToString());
                e.Data.RequestedOperation = DataPackageOperation.Move;
            };

            border.DragOver += (s, e) =>
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                var ptInBar = e.GetPosition(BarItemsPanel);
                UpdateDropIndicator(ptInBar);
            };

            border.Drop += async (s, e) =>
            {
                HideDropIndicator();
                if (_currentOpenFolderFlyout != null)
                {
                    _currentOpenFolderFlyout.Hide();
                    _currentOpenFolderFlyout = null;
                    _isAnyFlyoutOpen = false;
                }
                try
                {
                    if (e.DataView.Contains(StandardDataFormats.Text))
                    {
                        string data = await e.DataView.GetTextAsync();
                        if (data.StartsWith("bear-bookmark-id:"))
                        {
                            string idStr = data.Substring("bear-bookmark-id:".Length);
                            if (Guid.TryParse(idStr, out Guid srcId) && srcId != item.Id)
                            {
                                var srcItem = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == srcId);
                                if (srcItem != null)
                                {
                                    var p = e.GetPosition(border);
                                    bool insertBefore = p.X < (border.ActualWidth / 2.0);

                                    srcItem.Folder = "Sık kullanılanlar çubuğu";
                                    _bookmarkService.Bookmarks.Remove(srcItem);

                                    int targetIndex = _bookmarkService.Bookmarks.IndexOf(item);
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

                                    MoveBarOrderItem("bm:" + srcId.ToString(), "bm:" + item.Id.ToString(), insertBefore);
                                    await _bookmarkService.SaveChangesAsync();
                                    RefreshBar();
                                }
                            }
                        }
                        else if (data.StartsWith("bear-folder:"))
                        {
                            string srcFolder = data.Substring("bear-folder:".Length);
                            var p = e.GetPosition(border);
                            bool insertBefore = p.X < (border.ActualWidth / 2.0);

                            MoveBarOrderItem("folder:" + srcFolder, "bm:" + item.Id.ToString(), insertBefore);
                            await _bookmarkService.SaveChangesAsync();
                            RefreshBar();
                        }
                    }
                }
                catch { }
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var img = new Image
            {
                Width = 14,
                Height = 14
            };

            string effectiveFavicon = item.FaviconUrl;
            if (string.IsNullOrWhiteSpace(effectiveFavicon) && !string.IsNullOrWhiteSpace(item.Url))
            {
                try
                {
                    var u = new Uri(item.Url);
                    if (!string.IsNullOrEmpty(u.Host))
                        effectiveFavicon = $"https://icons.duckduckgo.com/ip3/{u.Host}.ico";
                }
                catch { }
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(effectiveFavicon))
                    img.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(effectiveFavicon));
            }
            catch { }

            sp.Children.Add(img);
            sp.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                MaxWidth = 130,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });

            border.Child = sp;

            border.Tapped += (s, e) =>
            {
                NavigateRequested?.Invoke(this, item.Url);
            };

            border.PointerPressed += (s, e) =>
            {
                var props = e.GetCurrentPoint(border).Properties;
                if (props.IsMiddleButtonPressed)
                {
                    e.Handled = true;
                    NewTabRequested?.Invoke(this, item.Url);
                }
            };

            var itemFlyout = new MenuFlyout();
            var newTabItem = new MenuFlyoutItem { Text = "Yeni sekmede aç" };
            newTabItem.Click += (s, e) => NewTabRequested?.Invoke(this, item.Url);
            var newWinItem = new MenuFlyoutItem { Text = "Yeni pencerede aç" };
            newWinItem.Click += (s, e) => NewWindowRequested?.Invoke(this, item.Url);
            var newInPrivateItem = new MenuFlyoutItem { Text = "Yeni InPrivate penceresinde aç" };
            newInPrivateItem.Click += (s, e) => NewInPrivateRequested?.Invoke(this, item.Url);

            var moveSub = new MenuFlyoutSubItem { Text = "Klasöre taşı" };
            var mainBarMoveItem = new MenuFlyoutItem { Text = "Sık kullanılanlar çubuğu" };
            mainBarMoveItem.Click += async (s, e) =>
            {
                item.Folder = "Sık kullanılanlar çubuğu";
                await _bookmarkService.SaveChangesAsync();
                RefreshBar();
            };
            moveSub.Items.Add(mainBarMoveItem);
            foreach (var f in _bookmarkService.Folders.Where(x => !x.Equals("Sık kullanılanlar çubuğu", StringComparison.OrdinalIgnoreCase)))
            {
                var fItem = new MenuFlyoutItem { Text = f };
                string targetF = f;
                fItem.Click += async (s, e) =>
                {
                    item.Folder = targetF;
                    await _bookmarkService.SaveChangesAsync();
                    RefreshBar();
                };
                moveSub.Items.Add(fItem);
            }

            var editMenuItem = new MenuFlyoutItem { Text = "Düzenle" };
            editMenuItem.Click += (s, e) => ShowEditBookmarkDialog(item);
            var delMenuItem = new MenuFlyoutItem { Text = "Sil" };
            delMenuItem.Click += async (s, e) =>
            {
                await _bookmarkService.RemoveBookmarkAsync(item.Id);
                RefreshBar();
            };

            itemFlyout.Items.Add(newTabItem);
            itemFlyout.Items.Add(newWinItem);
            itemFlyout.Items.Add(newInPrivateItem);
            itemFlyout.Items.Add(new MenuFlyoutSeparator());
            itemFlyout.Items.Add(moveSub);
            itemFlyout.Items.Add(editMenuItem);
            itemFlyout.Items.Add(delMenuItem);
            border.ContextFlyout = itemFlyout;

            return border;
        }

        private void BarRootGrid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            var pt = e.GetPosition(BarItemsPanel);
            UpdateDropIndicator(pt);
        }

        private void BarRootGrid_DragLeave(object sender, DragEventArgs e)
        {
            HideDropIndicator();
        }

        private async void BarRootGrid_Drop(object sender, DragEventArgs e)
        {
            HideDropIndicator();
            if (_currentOpenFolderFlyout != null)
            {
                _currentOpenFolderFlyout.Hide();
                _currentOpenFolderFlyout = null;
                _isAnyFlyoutOpen = false;
            }
            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string data = await e.DataView.GetTextAsync();
                    var pt = e.GetPosition(BarItemsPanel);
                    var (targetKey, insertBefore) = GetTargetBarOrderInfo(pt);

                    if (data.StartsWith("bear-bookmark-id:"))
                    {
                        string idStr = data.Substring("bear-bookmark-id:".Length);
                        if (Guid.TryParse(idStr, out Guid bId))
                        {
                            var item = _bookmarkService.Bookmarks.FirstOrDefault(b => b.Id == bId);
                            if (item != null)
                            {
                                item.Folder = "Sık kullanılanlar çubuğu";
                                MoveBarOrderItem("bm:" + item.Id.ToString(), targetKey, insertBefore);
                                await _bookmarkService.SaveChangesAsync();
                                RefreshBar();
                            }
                        }
                    }
                    else if (data.StartsWith("bear-folder:"))
                    {
                        string srcFolder = data.Substring("bear-folder:".Length);
                        MoveBarOrderItem("folder:" + srcFolder, targetKey, insertBefore);
                        await _bookmarkService.SaveChangesAsync();
                        RefreshBar();
                    }
                }
            }
            catch { }
        }

        private void MoveBarOrderItem(string srcKey, string? targetKey, bool insertBefore)
        {
            _bookmarkService.EnsureBarOrder();
            var list = _bookmarkService.BarOrder;
            if (string.IsNullOrEmpty(srcKey)) return;
            if (srcKey == targetKey) return;

            list.Remove(srcKey);

            if (!string.IsNullOrEmpty(targetKey))
            {
                int targetIdx = list.IndexOf(targetKey);
                if (targetIdx >= 0)
                {
                    if (!insertBefore) targetIdx++;
                    if (targetIdx <= list.Count)
                        list.Insert(targetIdx, srcKey);
                    else
                        list.Add(srcKey);
                    return;
                }
            }

            list.Add(srcKey);
        }

        private (string? targetKey, bool insertBefore) GetTargetBarOrderInfo(Windows.Foundation.Point ptInBar)
        {
            for (int i = 0; i < BarItemsPanel.Children.Count; i++)
            {
                if (BarItemsPanel.Children[i] is FrameworkElement child)
                {
                    var transform = child.TransformToVisual(BarItemsPanel);
                    var origin = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    double left = origin.X;
                    double width = child.ActualWidth;
                    double center = left + (width / 2.0);

                    if (ptInBar.X < center)
                    {
                        return (GetKeyForElement(child), true);
                    }
                }
            }

            if (BarItemsPanel.Children.Count > 0)
            {
                if (BarItemsPanel.Children[BarItemsPanel.Children.Count - 1] is FrameworkElement lastChild)
                {
                    return (GetKeyForElement(lastChild), false);
                }
            }

            return (null, false);
        }

        private string? GetKeyForElement(FrameworkElement element)
        {
            if (element.Tag is BookmarkItem bm)
            {
                return "bm:" + bm.Id.ToString();
            }
            else if (element.Tag is string folder)
            {
                return "folder:" + folder;
            }
            return null;
        }

        private void UpdateDropIndicator(Windows.Foundation.Point ptInBar)
        {
            if (DropIndicatorLine == null) return;

            if (BarItemsPanel.Children.Count == 0)
            {
                Canvas.SetLeft(DropIndicatorLine, 0);
                DropIndicatorLine.Visibility = Visibility.Visible;
                return;
            }

            double indicatorX = 0;
            bool found = false;

            for (int i = 0; i < BarItemsPanel.Children.Count; i++)
            {
                if (BarItemsPanel.Children[i] is FrameworkElement child)
                {
                    var transform = child.TransformToVisual(BarItemsPanel);
                    var origin = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    double left = origin.X;
                    double width = child.ActualWidth;
                    double center = left + (width / 2.0);

                    if (ptInBar.X < center)
                    {
                        indicatorX = Math.Max(0, left - 3);
                        found = true;
                        break;
                    }
                }
            }

            if (!found && BarItemsPanel.Children.Count > 0)
            {
                if (BarItemsPanel.Children[BarItemsPanel.Children.Count - 1] is FrameworkElement lastChild)
                {
                    var transform = lastChild.TransformToVisual(BarItemsPanel);
                    var origin = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    indicatorX = origin.X + lastChild.ActualWidth + 1;
                }
            }

            Canvas.SetLeft(DropIndicatorLine, indicatorX);
            DropIndicatorLine.Visibility = Visibility.Visible;
        }

        private void HideDropIndicator()
        {
            if (DropIndicatorLine != null)
            {
                DropIndicatorLine.Visibility = Visibility.Collapsed;
            }
        }

        private void AddFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowNewFolderDialog();
        }

        private void HideBarMenuItem_Click(object sender, RoutedEventArgs e)
        {
            HideFavoritesBarRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowNewFolderDialog()
        {
            NewFolderTextBox.Text = string.Empty;
            NewFolderFlyout.ShowAt(BarRootGrid);
            NewFolderTextBox.Focus(FocusState.Programmatic);
        }

        private async void ConfirmNewFolder_Click(object sender, RoutedEventArgs e)
        {
            string name = NewFolderTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                await _bookmarkService.AddFolderAsync(name);
            }
            NewFolderFlyout.Hide();
        }

        private void CancelNewFolder_Click(object sender, RoutedEventArgs e)
        {
            NewFolderFlyout.Hide();
        }

        private void NewFolderTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ConfirmNewFolder_Click(sender, e);
            }
        }

        private void ShowEditBookmarkDialog(BookmarkItem item)
        {
            _editingBookmark = item;
            EditNameTextBox.Text = item.Title;
            EditUrlTextBox.Text = item.Url;
            EditBookmarkFlyout.ShowAt(BarRootGrid);
        }

        private async void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_editingBookmark != null)
            {
                string title = EditNameTextBox.Text.Trim();
                string url = EditUrlTextBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    _editingBookmark.Title = string.IsNullOrWhiteSpace(title) ? url : title;
                    _editingBookmark.Url = url;
                    await _bookmarkService.SaveChangesAsync();
                }
            }
            EditBookmarkFlyout.Hide();
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            EditBookmarkFlyout.Hide();
        }
    }
}
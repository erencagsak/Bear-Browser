using System;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Bear_Browser.Controls
{
    public sealed partial class FavoritesBar : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public event EventHandler<string>? NewTabRequested;

        private readonly IBookmarkService _bookmarkService;

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
            BarItemsPanel.Children.Clear();

            // 1. Kullanıcının oluşturduğu alt klasörleri açılır menü butonu olarak ekle
            var userFolders = _bookmarkService.Folders
                .Where(f => !f.Equals("Sık kullanılanlar çubuğu", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var folder in userFolders)
            {
                var folderBookmarks = _bookmarkService.Bookmarks
                    .Where(b => b.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var dropBtn = new DropDownButton
                {
                    Height = 28,
                    Padding = new Thickness(6, 2, 6, 2),
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0)
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                sp.Children.Add(new FontIcon { Glyph = "\uED25", FontSize = 12 });
                sp.Children.Add(new TextBlock { Text = folder, FontSize = 12 });
                dropBtn.Content = sp;

                // AŞAĞI DOĞRU AÇILMAYI ZORLA (BottomEdgeAlignedLeft)
                var flyout = new MenuFlyout
                {
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                };

                if (folderBookmarks.Count == 0)
                {
                    flyout.Items.Add(new MenuFlyoutItem { Text = "(Klasör boş)", IsEnabled = false });
                }
                else
                {
                    foreach (var b in folderBookmarks)
                    {
                        var item = new MenuFlyoutItem { Text = b.Title, Tag = b.Url };
                        item.Icon = new FontIcon { Glyph = "\uE774" };
                        item.Click += (s, e) =>
                        {
                            if (s is MenuFlyoutItem mfi && mfi.Tag is string targetUrl)
                                NavigateRequested?.Invoke(this, targetUrl);
                        };
                        item.PointerPressed += (s, e) =>
                        {
                            var props = e.GetCurrentPoint(item).Properties;
                            if (props.IsMiddleButtonPressed)
                            {
                                e.Handled = true;
                                if (item.Tag is string targetUrl)
                                {
                                    NewTabRequested?.Invoke(this, targetUrl);
                                    flyout.Hide();
                                }
                            }
                        };
                        flyout.Items.Add(item);
                    }
                }
                dropBtn.Flyout = flyout;
                BarItemsPanel.Children.Add(dropBtn);
            }

            // 2. Doğrudan bu çubuğa eklenen siteler
            var directBookmarks = _bookmarkService.Bookmarks
                .Where(b => b.Folder.Equals("Sık kullanılanlar çubuğu", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in directBookmarks)
            {
                var btn = new Button
                {
                    Height = 28,
                    Padding = new Thickness(6, 2, 6, 2),
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Tag = item.Url
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var img = new Image
                {
                    Width = 14,
                    Height = 14,
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(item.FaviconUrl))
                };
                sp.Children.Add(img);
                sp.Children.Add(new TextBlock
                {
                    Text = item.Title,
                    FontSize = 12,
                    MaxWidth = 130,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                btn.Content = sp;
                btn.Click += (s, e) =>
                {
                    if (s is Button b && b.Tag is string url)
                        NavigateRequested?.Invoke(this, url);
                };
                btn.PointerPressed += (s, e) =>
                {
                    var props = e.GetCurrentPoint(btn).Properties;
                    if (props.IsMiddleButtonPressed)
                    {
                        e.Handled = true;
                        if (btn.Tag is string url)
                            NewTabRequested?.Invoke(this, url);
                    }
                };

                BarItemsPanel.Children.Add(btn);
            }
        }
    }
}
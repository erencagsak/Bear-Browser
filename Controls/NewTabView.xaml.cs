using System;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Bear_Browser.Controls
{
    public sealed partial class NewTabView : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public event EventHandler<string>? NewTabRequested;

        private readonly ILocalizationService _localizationService;
        private readonly ISettingsService _settingsService;
        private readonly DispatcherTimer _clockTimer;
        public bool IsInPrivate { get; }

        public NewTabView() : this(false)
        {
        }

        public NewTabView(bool isInPrivate)
        {
            IsInPrivate = isInPrivate;
            _localizationService = App.Services.GetRequiredService<ILocalizationService>();
            _settingsService = App.Services.GetRequiredService<ISettingsService>();

            InitializeComponent();
            ApplyModeState();
            ApplyLocalization();
            UpdateClockAndGreeting();

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => UpdateClockAndGreeting();
            _clockTimer.Start();

            _localizationService.LanguageChanged += (s, e) => DispatcherQueue?.TryEnqueue(ApplyLocalization);
            this.Loaded += NewTabView_Loaded;
            this.Unloaded += (s, e) => _clockTimer.Stop();
        }

        private void ApplyModeState()
        {
            if (IsInPrivate)
            {
                InPrivateContainer.Visibility = Visibility.Visible;
                NormalContentScroll.Visibility = Visibility.Collapsed;
                WallpaperSwitcherBtn.Visibility = Visibility.Collapsed;

                var grad = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1)
                };
                grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 10, 10, 12), Offset = 0.0 });
                grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 14, 11, 20), Offset = 0.5 });
                grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 8, 8, 10), Offset = 1.0 });
                WallpaperBackdrop.Background = grad;

                AmbientGlow.Opacity = 0.2;
            }
            else
            {
                InPrivateContainer.Visibility = Visibility.Collapsed;
                NormalContentScroll.Visibility = Visibility.Visible;
                WallpaperSwitcherBtn.Visibility = Visibility.Visible;
            }
        }

        private void UpdateClockAndGreeting()
        {
            var now = DateTime.Now;
            LiveClockText.Text = now.ToString("HH:mm");

            try
            {
                var trCulture = new System.Globalization.CultureInfo("tr-TR");
                LiveDateText.Text = now.ToString("d MMMM dddd", trCulture);
            }
            catch
            {
                LiveDateText.Text = now.ToString("d MMMM dddd");
            }

            int hour = now.Hour;
            if (hour >= 5 && hour < 12)
            {
                GreetingText.Text = "Günaydın ☀️";
            }
            else if (hour >= 12 && hour < 18)
            {
                GreetingText.Text = "İyi Günler 🌿";
            }
            else if (hour >= 18 && hour < 23)
            {
                GreetingText.Text = "İyi Akşamlar 🌙";
            }
            else
            {
                GreetingText.Text = "İyi Geceler ✨";
            }
        }

        public void ApplyLocalization()
        {
            var loc = _localizationService;
            if (SearchBox != null) SearchBox.PlaceholderText = loc["NewTab_SearchPlaceholder"];
            if (SearchSubmitBtn != null) ToolTipService.SetToolTip(SearchSubmitBtn, loc["NewTab_SearchTooltip"]);
            if (QuickAccessTitleText != null) QuickAccessTitleText.Text = loc["NewTab_QuickAccess"];
        }

        private void NewTabView_Loaded(object sender, RoutedEventArgs e)
        {
            FocusSearchBox();
        }

        public void FocusSearchBox()
        {
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (IsInPrivate)
                {
                    InPrivateSearchBox.Focus(FocusState.Programmatic);
                    InPrivateSearchBox.SelectAll();
                }
                else
                {
                    SearchBox.Focus(FocusState.Programmatic);
                    SearchBox.SelectAll();
                }
            });
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                if (ReferenceEquals(sender, InPrivateSearchBox))
                    ProcessSearch(InPrivateSearchBox.Text);
                else
                    ProcessSearch(SearchBox.Text);
            }
        }

        private void SearchSubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessSearch(SearchBox.Text);
        }

        private void InPrivateSearchSubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessSearch(InPrivateSearchBox.Text);
        }

        private void ProcessSearch(string? rawQuery)
        {
            string query = rawQuery?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query)) return;

            string targetUrl;
            if (query.StartsWith("bear://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                query.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = query;
            }
            else if (query.Contains(".") && !query.Contains(" "))
            {
                targetUrl = "https://" + query;
            }
            else
            {
                targetUrl = _settingsService.GetSearchUrl(query);
            }

            NavigateRequested?.Invoke(this, targetUrl);
        }

        private void Shortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url)
            {
                NavigateRequested?.Invoke(this, url);
            }
        }

        private void Shortcut_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(sender as UIElement).Properties;
            if (props.IsMiddleButtonPressed)
            {
                e.Handled = true;
                if (sender is Button btn && btn.Tag is string url)
                {
                    NewTabRequested?.Invoke(this, url);
                }
            }
        }

        private void ChangeWallpaper_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var grad = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1)
                };

                switch (tag)
                {
                    case "bear":
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 28, 18, 12), Offset = 0.0 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 38, 24, 15), Offset = 0.4 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 26, 16, 9), Offset = 0.8 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 17, 10, 5), Offset = 1.0 });
                        break;
                    case "cosmic":
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 15, 16, 38), Offset = 0.0 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 22, 24, 56), Offset = 0.4 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 9, 27, 42), Offset = 0.8 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 5, 11, 20), Offset = 1.0 });
                        break;
                    case "sunset":
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 45, 15, 30), Offset = 0.0 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 30, 18, 55), Offset = 0.5 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 12, 10, 24), Offset = 1.0 });
                        break;
                    case "cyber":
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 8, 25, 35), Offset = 0.0 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 10, 18, 38), Offset = 0.5 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 4, 8, 16), Offset = 1.0 });
                        break;
                    case "slate":
                    default:
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 24, 24, 27), Offset = 0.0 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 18, 18, 20), Offset = 0.5 });
                        grad.GradientStops.Add(new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(255, 9, 9, 11), Offset = 1.0 });
                        break;
                }

                WallpaperBackdrop.Background = grad;
            }
        }
    }
}

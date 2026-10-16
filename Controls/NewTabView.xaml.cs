using System;
using Bear_Browser.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Bear_Browser.Controls
{
    public sealed partial class NewTabView : UserControl
    {
        public event EventHandler<string>? NavigateRequested;
        public event EventHandler<string>? NewTabRequested;

        private readonly ILocalizationService _localizationService;
        private readonly ISettingsService _settingsService;

        public NewTabView()
        {
            _localizationService = App.Services.GetRequiredService<ILocalizationService>();
            _settingsService = App.Services.GetRequiredService<ISettingsService>();

            InitializeComponent();
            ApplyLocalization();
            _localizationService.LanguageChanged += (s, e) => DispatcherQueue?.TryEnqueue(ApplyLocalization);
            this.Loaded += NewTabView_Loaded;
        }

        public void ApplyLocalization()
        {
            var loc = _localizationService;
            if (TaglineText != null) TaglineText.Text = loc["NewTab_Tagline"];
            if (SearchBox != null) SearchBox.PlaceholderText = loc["NewTab_SearchPlaceholder"];
            if (SearchSubmitBtn != null) ToolTipService.SetToolTip(SearchSubmitBtn, loc["NewTab_SearchTooltip"]);
            if (QuickAccessTitleText != null) QuickAccessTitleText.Text = loc["NewTab_QuickAccess"];
        }

        private void NewTabView_Loaded(object sender, RoutedEventArgs e)
        {
            // Sekme açıldığı anda klavye odağını arama çubuğuna ver
            FocusSearchBox();
        }

        public void FocusSearchBox()
        {
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                SearchBox.Focus(FocusState.Programmatic);
                SearchBox.SelectAll();
            });
        }

        private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                ProcessSearch();
            }
        }

        private void SearchSubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            ProcessSearch();
        }

        private void ProcessSearch()
        {
            string query = SearchBox.Text?.Trim() ?? "";
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
    }
}

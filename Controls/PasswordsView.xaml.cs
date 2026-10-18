using System;
using System.Collections.Generic;
using Bear_Browser.Models;
using Bear_Browser.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Bear_Browser.Controls
{
    public sealed partial class PasswordsView : UserControl
    {
        public PasswordsViewModel ViewModel { get; }

        public PasswordsView()
        {
            InitializeComponent();
            ViewModel = App.Services.GetRequiredService<PasswordsViewModel>();
            PasswordsListView.ItemsSource = ViewModel.DisplayedPasswords;

            ViewModel.DisplayedPasswords.CollectionChanged += (s, e) => UpdateEmptyState();
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            bool isEmpty = ViewModel.DisplayedPasswords.Count == 0;
            EmptyStateContainer.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            PasswordsListView.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            if (CountBadgeText != null)
            {
                CountBadgeText.Text = $"{ViewModel.DisplayedPasswords.Count} hesap";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
            UpdateEmptyState();
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PasswordItem item)
            {
                ViewModel.TogglePasswordVisibility(item);
            }
        }

        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pass && !string.IsNullOrEmpty(pass))
            {
                var package = new DataPackage();
                package.SetText(pass);
                Clipboard.SetContent(package);
            }
        }

        private async void DeletePassword_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Guid id)
            {
                await ViewModel.RemovePasswordAsync(id);
                UpdateEmptyState();
            }
        }

        private async void ClearAllPasswords_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Tüm Parolaları Sil",
                Content = "Kayıtlı tüm web sitesi parolalarınız kalıcı olarak silinecek. Onaylıyor musunuz?",
                PrimaryButtonText = "Evet, Tümünü Sil",
                CloseButtonText = "İptal",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.ClearPasswordsAsync();
                UpdateEmptyState();
            }
        }

        private async void ImportPasswordsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
                picker.FileTypeFilter.Add(".csv");
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    int count = await ViewModel.ImportPasswordsCsvAsync(file.Path);
                    UpdateEmptyState();
                    var dialog = new ContentDialog
                    {
                        Title = "Parolalar İçe Aktarıldı",
                        Content = count > 0
                            ? $"{count} adet kayıtlı hesap/parola başarıyla içe aktarıldı."
                            : "CSV dosyasında geçerli hesap bilgisi bulunamadı.",
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

        private async void ExportPasswordsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("CSV Dosyası (*.csv)", new List<string> { ".csv" });
                picker.SuggestedFileName = $"bear_passwords_{DateTime.Now:yyyyMMdd}";
                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await ViewModel.ExportPasswordsCsvAsync(file.Path);
                    var dialog = new ContentDialog
                    {
                        Title = "Parolalar Dışa Aktarıldı",
                        Content = $"Tüm parolalarınız başarıyla CSV dosyasına kaydedildi:\n{file.Path}",
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
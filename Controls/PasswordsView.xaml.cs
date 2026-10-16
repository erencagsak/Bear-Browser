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
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchQuery = SearchBox.Text?.Trim() ?? "";
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
            }
        }

        private async void ClearAllPasswords_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ClearPasswordsAsync();
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
using System;
using System.Linq;
using Bear_Browser.Models;
using Bear_Browser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Bear_Browser.Controls
{
    public sealed partial class PasswordsView : UserControl
    {
        public PasswordsView()
        {
            InitializeComponent();
            RefreshList();
            ProfileService.Instance.PasswordsUpdated += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(RefreshList);
            };
        }

        private void RefreshList()
        {
            string query = SearchBox.Text?.Trim() ?? "";
            var items = ProfileService.Instance.Passwords.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
            {
                items = items.Where(p =>
                    p.Domain.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.UserName.Contains(query, StringComparison.OrdinalIgnoreCase));
            }
            PasswordsListView.ItemsSource = items.ToList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PasswordItem item)
            {
                item.IsPasswordVisible = !item.IsPasswordVisible;
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
                await ProfileService.Instance.RemovePasswordAsync(id);
                RefreshList();
            }
        }

        private async void ClearAllPasswords_Click(object sender, RoutedEventArgs e)
        {
            await ProfileService.Instance.ClearPasswordsAsync();
            RefreshList();
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
                    int count = await ProfileService.Instance.ImportPasswordsCsvAsync(file.Path);
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
                picker.FileTypeChoices.Add("CSV Dosyası (*.csv)", new System.Collections.Generic.List<string> { ".csv" });
                picker.SuggestedFileName = $"bear_passwords_{DateTime.Now:yyyyMMdd}";
                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await ProfileService.Instance.ExportPasswordsCsvAsync(file.Path);
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

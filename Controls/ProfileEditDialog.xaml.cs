using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Bear_Browser.Models;
using Bear_Browser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Bear_Browser.Controls
{
    public sealed partial class ProfileEditDialog : ContentDialog
    {
        private bool _hasNewImage = false;
        private Point _lastPointerPosition;
        private bool _isDragging = false;

        public ProfileEditDialog(UserProfile currentProfile)
        {
            InitializeComponent();

            ProfileNameBox.Text = currentProfile.Name;
            AvatarPreview.DisplayName = currentProfile.Name;

            if (!string.IsNullOrEmpty(currentProfile.AvatarImagePath) && File.Exists(currentProfile.AvatarImagePath))
            {
                AvatarPreview.ProfilePicture = new BitmapImage(new Uri(currentProfile.AvatarImagePath));
            }
        }

        private async void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();

            // WinUI 3 masaüstü pencere kimliğini bağla
            WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);

            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".webp");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _hasNewImage = true;
                var bitmap = new BitmapImage(new Uri(file.Path));
                SourceCropImage.Source = bitmap;

                // Konumu ve yakınlaştırmayı sıfırla
                ImageTransform.TranslateX = 0;
                ImageTransform.TranslateY = 0;
                ImageTransform.ScaleX = 1.0;
                ImageTransform.ScaleY = 1.0;
                ZoomSlider.Value = 1.0;

                InitialAvatarPanel.Visibility = Visibility.Collapsed;
                CropEditorPanel.Visibility = Visibility.Visible;
            }
        }

        private void CropArea_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el)
            {
                _isDragging = true;
                _lastPointerPosition = e.GetCurrentPoint(el).Position;
                el.CapturePointer(e.Pointer);
            }
        }

        private void CropArea_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDragging || !(sender is UIElement el)) return;

            var currentPos = e.GetCurrentPoint(el).Position;
            double deltaX = currentPos.X - _lastPointerPosition.X;
            double deltaY = currentPos.Y - _lastPointerPosition.Y;
            _lastPointerPosition = currentPos;

            ImageTransform.TranslateX += deltaX;
            ImageTransform.TranslateY += deltaY;
        }

        private void CropArea_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is UIElement el)
            {
                _isDragging = false;
                el.ReleasePointerCapture(e.Pointer);
            }
        }

        private void CropArea_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            int delta = e.GetCurrentPoint(sender as UIElement).Properties.MouseWheelDelta;
            double step = delta > 0 ? 0.1 : -0.1;
            ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + step, ZoomSlider.Minimum, ZoomSlider.Maximum);
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ImageTransform != null)
            {
                ImageTransform.ScaleX = e.NewValue;
                ImageTransform.ScaleY = e.NewValue;
            }
        }

        private void ResetPosition_Click(object sender, RoutedEventArgs e)
        {
            if (ImageTransform != null)
            {
                ImageTransform.TranslateX = 0;
                ImageTransform.TranslateY = 0;
            }
            if (ZoomSlider != null)
            {
                ZoomSlider.Value = 1.0;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string newName = ProfileNameBox.Text.Trim();

            if (_hasNewImage)
            {
                try
                {
                    var rtb = new RenderTargetBitmap();
                    await rtb.RenderAsync(VisibleCircleCrop);
                    var pixelBuffer = await rtb.GetPixelsAsync();
                    byte[] pixels = pixelBuffer.ToArray();

                    string profileFolder = ProfileService.Instance.CurrentProfile.ProfileFolderPath;
                    string avatarFileName = $"avatar_{DateTime.UtcNow.Ticks}.png";
                    string destPath = Path.Combine(profileFolder, avatarFileName);

                    // Eski avatar dosyalarını temizle
                    foreach (var old in Directory.GetFiles(profileFolder, "avatar*"))
                    {
                        try { File.Delete(old); } catch { }
                    }

                    var folder = await StorageFolder.GetFolderFromPathAsync(profileFolder);
                    var file = await folder.CreateFileAsync(avatarFileName, CreationCollisionOption.ReplaceExisting);

                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                        encoder.SetPixelData(
                            BitmapPixelFormat.Bgra8,
                            BitmapAlphaMode.Premultiplied,
                            (uint)rtb.PixelWidth,
                            (uint)rtb.PixelHeight,
                            96, 96,
                            pixels);
                        await encoder.FlushAsync();
                    }

                    await ProfileService.Instance.UpdateProfileAvatarPathAsync(newName, destPath);
                }
                catch
                {
                    await ProfileService.Instance.UpdateProfileNameAsync(newName);
                }
            }
            else
            {
                await ProfileService.Instance.UpdateProfileNameAsync(newName);
            }
        }
    }
}
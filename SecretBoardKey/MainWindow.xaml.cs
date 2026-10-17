using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecretBoardKey
{
    public sealed partial class MainWindow : Window
    {
        private readonly KeyBoardHookService _hookService;
        private readonly ExactDecrypter _decrypter;
        private readonly UnicodeDecrypter _alphabetMain = new UnicodeDecrypter(UnicodeReserveAlphabet.isUnicode);
        private const string BackgroundFileName = "custom_background.jpg"; 

        public MainWindow()
        {
            this.InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(CustomTitleBar);
            _ = TryLoadSavedBackgroundAsync();
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.Loaded += MyWindow_Loaded;
            }

            string iconPath = System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets/Images/icoIconMain.ico");
            this.AppWindow.SetIcon(iconPath);

            _hookService = new KeyBoardHookService();

            _decrypter = new ExactDecrypter(chipherAlphabetReverse.TochipherVk);
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.IsResizable = false;
                overlappedPresenter.IsMaximizable = false;
                appWindow.Resize(new Windows.Graphics.SizeInt32(800, 500));
            }
            this.Closed += MainWindow_Closed;
        }

        private async void NewWindow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var secondWindow = new SecondWindow();
                secondWindow.Activate();
            }
            catch (Exception ex)
            {
                File.WriteAllText("Open_new_window_error.txt", ex.ToString());
            }
        }

        //изменения строки заголовка(сверху тонкая линия)
        private async void SelectBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    StorageFolder localFolder = await GetAppLocalFolderAsync();
                    StorageFile savedFile = await file.CopyAsync(localFolder, BackgroundFileName, NameCollisionOption.ReplaceExisting);

                    await SetBackgroundAsync(savedFile);
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("background_change_error.txt", ex.ToString());
            }
        }

        private async Task TryLoadSavedBackgroundAsync()
        {
            try
            {
                StorageFolder localFolder = await GetAppLocalFolderAsync();
                IStorageItem item = await localFolder.TryGetItemAsync(BackgroundFileName);
                if (item is StorageFile savedFile)
                {
                    await SetBackgroundAsync(savedFile);
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("file_read_error.txt", ex.ToString());
            }
        }

        private async Task<StorageFolder> GetAppLocalFolderAsync()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(localAppData, "SecretBoardKey");
            Directory.CreateDirectory(appFolder);

            return await StorageFolder.GetFolderFromPathAsync(appFolder);
        }

        private async Task SetBackgroundAsync(StorageFile File)
        {
            using (var stream = await File.OpenAsync(FileAccessMode.Read))
            {
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(stream);
                BackgroundImage.Source = bitmapImage;
            }
        }
        private void TextEnter_Changed(object sender, TextChangedEventArgs e)
        {
            string encryptedText = EnterText.Text;
            if (string.IsNullOrEmpty(encryptedText))
            {
                outPutText.Text = string.Empty;
                return;
            }
            string decrypterOutPutText = _decrypter.Decrypt(encryptedText);
            outPutText.Text = decrypterOutPutText;
        }
        private void TextEnter_ChangeToUnicode(object sender, TextChangedEventArgs e)
        {
            try
            {
                string originalText = EnterTextTwo.Text;
                if (string.IsNullOrEmpty(originalText))
                {
                    outPutTextUnicode.Text = string.Empty;
                    return;
                }
                if (_alphabetMain == null)
                    throw new InvalidOperationException("_alphabetMain is null");
                string decryptedTextUnicode = _alphabetMain.DecryptToUnicode(originalText);
                outPutTextUnicode.Text = decryptedTextUnicode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                outPutTextUnicode.Text = "Ошибка: " + ex.Message;
            }
        }

        private void HookToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (HookToggleSwitch.IsOn)
            {
                bool success = _hookService.Start();
                if (success)
                {
                    StatusTextBlock.Text = "Хук успешно запущен!";
                }
                else
                {
                    StatusTextBlock.Text = "Ошибка запуска хука.";
                    HookToggleSwitch.IsOn = false;
                }
            }
            else
            {
                _hookService.Stop();
                StatusTextBlock.Text = "Хук остановлен.";
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _hookService.Stop();
        }

        // это изменяет слева прямоугольное фото
        private async void ReplaceImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker openPickerTwo = new FileOpenPicker();

                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(openPickerTwo, hwnd);

                openPickerTwo.ViewMode = PickerViewMode.Thumbnail;
                openPickerTwo.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                openPickerTwo.FileTypeFilter.Add(".jpg");
                openPickerTwo.FileTypeFilter.Add(".jpeg");
                openPickerTwo.FileTypeFilter.Add(".png");
                StorageFile fileTwo = await openPickerTwo.PickSingleFileAsync();
                if (fileTwo != null)
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    using (var stream = await fileTwo.OpenAsync(FileAccessMode.Read))
                    {
                        await bitmapImage.SetSourceAsync(stream);
                    }
                    SimpleImage.Source = bitmapImage;
                    string token = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(fileTwo);
                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["LastPickedFileToken"] = token;
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("background_change_error.txt", ex.ToString());
            }
        }

        private async void MyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (!localSettings.Values.ContainsKey("LastPickedFileToken")) return;
            string token = localSettings.Values["LastPickedFileToken"]?.ToString();
            try
            {
                // Спокойно используем await, так как метод помечен как async void
                StorageFile savedFile = await Windows.Storage.AccessCache.StorageApplicationPermissions
                    .FutureAccessList.GetFileAsync(token);

                BitmapImage bitmapImage = new BitmapImage();
                using (var stream = await savedFile.OpenAsync(FileAccessMode.Read))
                {
                    await bitmapImage.SetSourceAsync(stream);
                }
                SimpleImage.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при автозагрузке: {ex.Message}");
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SecretBoardKey;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SecondWindow : Window
{
    public SecondWindow()
    {
        InitializeComponent();
        LoadImagesList();
        SetTitleBar(CustomTitleBar);
        ExtendsContentIntoTitleBar = true;

        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
        {
            overlappedPresenter.IsResizable = false;
            overlappedPresenter.IsMaximizable = false;
            appWindow.Resize(new Windows.Graphics.SizeInt32(500, 250));
        }
    }

    private void LoadImagesList()
    {
        try
        {
            //уПолучаем путь к папке Assets/images внутри установленного .msix
            string rootPath = Package.Current.InstalledLocation.Path;
            string imagesFolderPath = Path.Combine(rootPath, "Assets", "images");

            if (Directory.Exists(imagesFolderPath))
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

                var fileNames = Directory.GetFiles(imagesFolderPath)
                    .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLower()))
                    .Select(file => Path.GetFileName(file))
                    .ToList();

                ImagesListView.ItemsSource = fileNames;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки файлов: {ex.Message}");
        }
    }

    // Событие клика/выбора элемента в списке
    private void ImagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImagesListView.SelectedItem is string selectedFileName)
        {
            // 4. Открываем выбранную картинку по её имени через схему ms-appx:///
            string imageUriPath = $"ms-appx:///Assets/images/{selectedFileName}";

            TargetImage.Source = new BitmapImage(new Uri(imageUriPath));
        }
    }

    internal void Show()
    {
        throw new NotImplementedException();
    }
}

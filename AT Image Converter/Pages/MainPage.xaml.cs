using CommunityToolkit.Mvvm.Messaging;
using ImageConverterAT.ViewModels;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ImageConverterAT.Pages;

public sealed partial class MainPage : Page,
    IRecipient<ShowFileOpenPickerMessage>,
    IRecipient<ShowFolderPickerMessage>,
    IRecipient<ResetImagePreviewZoomMessage>,
    IRecipient<ZoomChangedMessage>
{
    private static readonly HashSet<string> s_nonNativePreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".psd", ".xcf", ".raw", ".pdf", ".svg"
    };

    public static MainPage Instance { get; private set; }

    public MainPageViewModel ViewModel { get; }

    private readonly ResourceLoader _resourceLoader = new();
    private CancellationTokenSource _previewCancellationTokenSource;

    public MainPage()
    {
        Instance = this;

        ViewModel = new MainPageViewModel();
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<ShowFileOpenPickerMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowFolderPickerMessage>(this);
        WeakReferenceMessenger.Default.Register<ResetImagePreviewZoomMessage>(this);
        WeakReferenceMessenger.Default.Register<ZoomChangedMessage>(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string[] launchFilePaths && launchFilePaths.Length > 0) WeakReferenceMessenger.Default.Send(new AddImageFilesMessage(launchFilePaths));
    }

    public async void Receive(ShowFileOpenPickerMessage _)
    {
        var filePicker = new FileOpenPicker(MainWindow.Instance.AppWindow.Id);
        foreach (var imageFileFormat in Constants.ImageFileFormats) filePicker.FileTypeFilter.Add(imageFileFormat);
        filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var files = await filePicker.PickMultipleFilesAsync();
        var filePaths = files.Select(file => file.Path).ToList();
        if (filePaths.Count > 0) WeakReferenceMessenger.Default.Send(new AddImageFilesMessage(filePaths));
    }

    public async void Receive(ShowFolderPickerMessage _)
    {
        var folderPicker = new FolderPicker(MainWindow.Instance.AppWindow.Id);
        folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var folder = await folderPicker.PickSingleFolderAsync();
        WeakReferenceMessenger.Default.Send(new FolderSelectedMessage(folder?.Path ?? ""));
    }

    public void Receive(ResetImagePreviewZoomMessage message) => ResetImagePreviewZoomFactor(message.Dimensions.Width, message.Dimensions.Height);

    public void Receive(ZoomChangedMessage message)
    {
        var zoomFactor = PreviewScrollViewer.ZoomFactor;
        var delta = message.IsZoomIn ? 0.1 : -0.1;
        PreviewScrollViewer.ChangeView(null, null, (float)(zoomFactor + delta));
    }

    private void ResetImagePreviewZoomFactor(double? width = null, double? height = null)
    {
        if (width is 0 or null) width = PreviewImage.ActualWidth;
        if (height is 0 or null) height = PreviewImage.ActualHeight;

        var zoomFactor = Math.Min(PreviewScrollViewer.ActualWidth / width.Value, PreviewScrollViewer.ActualHeight / height.Value);
        if (double.IsNaN(zoomFactor) || double.IsInfinity(zoomFactor)) return;

        PreviewScrollViewer.ChangeView(null, null, (float)zoomFactor);
    }

    private async void OnImageListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var imageFileViewModel = ViewModel.SelectedImageFile;
        if (imageFileViewModel == null) return;

        var imageFilePath = imageFileViewModel.FilePath;
        var extension = Path.GetExtension(imageFilePath);

        PreviewImage.Source = null;

        if (!s_nonNativePreviewExtensions.Contains(extension))
        {
            var bitmapImage = new BitmapImage { UriSource = new Uri(imageFilePath) };
            PreviewImage.Source = bitmapImage;
            return;
        }

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && !IsGhostscriptInstalled()) return;

        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource?.Dispose();
        _previewCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _previewCancellationTokenSource.Token;

        MainWindow.Instance.ShowLoading(_resourceLoader.GetString("PreviewLoading"));
        await LoadNonNativePreviewAsync(imageFileViewModel, cancellationToken);
    }

    private async Task LoadNonNativePreviewAsync(ImageFileViewModel imageFileViewModel, CancellationToken cancellationToken)
    {
        try
        {
            var imageBytes = await Task.Run(() =>
            {
                using var image = imageFileViewModel.CreateMagickImage();
                cancellationToken.ThrowIfCancellationRequested();
                return image.ToByteArray(MagickFormat.Png);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            using var stream = new InMemoryRandomAccessStream();
            using var dataWriter = new DataWriter(stream.GetOutputStreamAt(0));
            dataWriter.WriteBytes(imageBytes);
            await dataWriter.StoreAsync();
            stream.Seek(0);

            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(stream);
            PreviewImage.Source = bitmapImage;

            ResetImagePreviewZoomFactor(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { PreviewImage.Source = null; }
        finally { MainWindow.Instance.HideLoading(); }
    }

    private static bool IsGhostscriptInstalled()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var baseDir in new[] { programFiles, programFilesX86 })
        {
            var gsDir = Path.Combine(baseDir, "gs");
            if (!Directory.Exists(gsDir)) continue;

            try
            {
                if (Directory.EnumerateFiles(gsDir, "gswin*c.exe", SearchOption.AllDirectories).Any())
                {
                    return true;
                }
            }
            catch { }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir, "gswin64c.exe")) || File.Exists(Path.Combine(dir, "gswin32c.exe")))
                {
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    private void OnImagePreviewImageOpened(object sender, RoutedEventArgs e) => ResetImagePreviewZoomFactor();

    private void OnPreviewScrollViewerSizeChanged(object sender, SizeChangedEventArgs e) => ResetImagePreviewZoomFactor();

    private void OnDropPlaceholderButtonClicked(object sender, RoutedEventArgs e) => WeakReferenceMessenger.Default.Send(ShowFileOpenPickerMessage.Instance);

    private void OnDropPlaceholderButtonDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDropPlaceholderButtonDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        var supportedExtensions = new HashSet<string>(Constants.ImageFileFormats, StringComparer.OrdinalIgnoreCase);

        var filePaths = items
            .OfType<StorageFile>()
            .Where(file => supportedExtensions.Contains(file.FileType))
            .Select(file => file.Path)
            .ToList();

        if (filePaths.Count > 0) WeakReferenceMessenger.Default.Send(new AddImageFilesMessage(filePaths));
    }

    private void OnAddImageAppBarButtonKeyboardAcceleratorInvoked(object sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        e.Handled = true;
        WeakReferenceMessenger.Default.Send(ShowFileOpenPickerMessage.Instance);
    }

    private async void OnLanguageRadioMenuFlyoutItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem selectedLanguageMenuFlyoutItem) return;
        if (selectedLanguageMenuFlyoutItem.Tag is not string selectedLanguageTag) return;
        await ViewModel.ChangeLanguageCommand.ExecuteAsync(selectedLanguageTag);
    }
}
using ImageConverterAT.Enums;
using ImageConverterAT.ViewModels;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage.Pickers;
namespace ImageConverterAT;

public sealed partial class MainWindow : Window
{
    private readonly bool _isInitialized = false;
    private readonly ObservableCollection<ImageFileViewModel> _imageFileViewModels = [];
    private readonly ObservableCollection<string> _progressLog = [];
    private readonly ResourceLoader _resourceLoader = new();

	public MainWindow()
	{
        InitializeComponent();
        _isInitialized = true;

        // Setup window
		AppWindow.SetIcon("Assets/Icon.ico");
		ExtendsContentIntoTitleBar = true;

		// Add files from command line arguments if any
		var args = Environment.GetCommandLineArgs();
		if (args.Length > 1) AddImageFiles(args[1..]); // Skip first argument, which is the executable path

        // Assign data source to list view
        LvImages.ItemsSource = _imageFileViewModels;

        LvProgressLog.ItemsSource = _progressLog;

        // Select first item programmatically to trigger selection changed event after initialization
        CbxSizeSettings.SelectedIndex = 0;
        CbxSizeUnit.SelectedIndex = 0;
    }

    private void AddImageFiles(IEnumerable<string> paths)
	{
        // Get previous count to know if we need to select the first item after adding
        var previousCount = _imageFileViewModels.Count;

        // Create view models, excluding duplicates
        var existingPaths = new HashSet<string>(_imageFileViewModels.Select(viewModel => viewModel.FilePath));

        var newViewModels = paths
            .Where(existingPaths.Add)
            .Select(path => new ImageFileViewModel(path))
            .ToList();

        if (newViewModels.Count == 0) return;

        // Detach ItemsSource to prevent UI updates on each Add
        LvImages.ItemsSource = null;

        foreach (var viewModel in newViewModels) _imageFileViewModels.Add(viewModel);

        // Sort in-place: rebuild the collection in sorted order
        var sorted = _imageFileViewModels.OrderBy(viewModel => viewModel.FileName, StringComparer.Ordinal).ToList();
        _imageFileViewModels.Clear();
        foreach (var viewModel in sorted) _imageFileViewModels.Add(viewModel);

        // Reattach ItemsSource so UI renders once
        LvImages.ItemsSource = _imageFileViewModels;

        // Select first item and update prefix format preview text box if there was no item before
        if (previousCount == 0)
        {
            LvImages.SelectedIndex = 0;
            UpdatePrefixFormatPreviewTextBox();
        }
    }

    private void UpdatePrefixFormatPreviewTextBox()
    {
        var currentFileName = _selectedImageFileViewModel?.FileName;

        if (currentFileName == null)
        {
            TbxPrefixPreview.Text = "";
            return;
        }

        TbxPrefixPreview.Text = GetSavedFileName(_selectedImageFileViewModel);
    }

    private string GetSavedFileName(ImageFileViewModel viewModel)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(viewModel.FileName);
        var format = GetCurrentOutputFormat();
        var fileName = TbxPrefix.Text + fileNameWithoutExtension + format;
        return fileName;
    }

    private string GetCurrentOutputFormat() => "." + (CbxFormat.SelectedItem as string).ToLower();

    private void ResetImagePreviewZoomFactor()
    {
        var zoomFactor = Math.Min(SvPreview.ActualWidth / ImgPreview.ActualWidth, SvPreview.ActualHeight / ImgPreview.ActualHeight);
        if (double.IsNaN(zoomFactor)) return;
        if (double.IsInfinity(zoomFactor)) return;

        SvPreview.ChangeView(null, null, (float)zoomFactor);
    }

    private async Task ConvertImagesAsync()
    {
        // TODO: Convert images
        FrPreview.IsEnabled = false;
        SvSettings.IsEnabled = false;
        BtConvert.IsEnabled = false;
        GdProgress.Visibility = Visibility.Visible;
        AddProgressLog(_resourceLoader.GetString("ConversionStarted"));

    var formatName = CbxFormat.SelectedItem as string;
        foreach (var viewModel in _imageFileViewModels.ToList())
        {
            var directoryPath = Path.GetDirectoryName(viewModel.FilePath);
            DispatcherQueue.TryEnqueue(() => LvImages.SelectedItem = viewModel);
            AddProgressLog(string.Format(_resourceLoader.GetString("ConvertingFile"), viewModel.FileName));

            var fileName = GetSavedFileName(viewModel);
            var filePath = Path.Combine(directoryPath, fileName);

            using var image = viewModel.CreateMagickImage();
            var sizeSetting = (SizeSetting)CbxSizeSettings.SelectedIndex;
            var sizeUnit = (SizeUnit)CbxSizeUnit.SelectedIndex;
            var width = (uint)NbxWidth.Value;
            var height = (uint)NbxHeight.Value;

            if (formatName != "ICO") await Task.Run(() => ResizeImage(image, sizeSetting, sizeUnit, width, height));
            // Convert to ico format if selected
            else
            {
                using var collection = new MagickImageCollection();

                // Define icon sizes (all available size for ico format)
                uint[] sizes = [16, 32, 48, 64, 128, 256];

                // Generate icon images with different sizes
                foreach (var size in sizes)
                {
                    var iconImage = new MagickImage();

                    if (Path.GetExtension(viewModel.FilePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                        iconImage.BackgroundColor = MagickColors.Transparent;

                    iconImage.Read(viewModel.FilePath);
                    iconImage.Resize(size, size);
                    collection.Add(iconImage);
                }

                collection.Write(filePath, MagickFormat.Ico);
                continue;
            }

            if (formatName == "JPG")
            {
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)NbQuality.Value;
            }
            else if (formatName == "JXL")
            {
                image.Format = MagickFormat.Jxl;
                image.Quality = (uint)NbQuality.Value;
            }
            else if (formatName == "WEBP")
            {
                image.Format = MagickFormat.WebP;
                image.Quality = (uint)NbQuality.Value;
            }
            else if (formatName == "AVIF")
            {
                image.Format = MagickFormat.Avif;
                image.Quality = (uint)NbQuality.Value;
            }
            else if (formatName == "HEIF")
            {
                image.Format = MagickFormat.Heif;
                image.Quality = (uint)NbQuality.Value;
            }
            else if (formatName == "TIFF")
            {
                image.Format = MagickFormat.Tiff;
                image.Quality = (uint)NbQuality.Value;
            }
            else if (formatName == "PNG") image.Format = MagickFormat.Png;
            else if (formatName == "BMP") image.Format = MagickFormat.Bmp;

            await Task.Run(() => image.Write(filePath));
            AddProgressLog(string.Format(_resourceLoader.GetString("ConversionFileComplete"), viewModel.FileName, fileName));
        }

        AddProgressLog(_resourceLoader.GetString("ConversionComplete"));

        await FrMain.ShowMessageDialogAsync(
            string.Format(_resourceLoader.GetString("ConversionCompleteDialogContent"), _imageFileViewModels.Count),
            _resourceLoader.GetString("ConversionCompleteDialogTitle"));

        FrPreview.IsEnabled = true;
        SvSettings.IsEnabled = true;
        BtConvert.IsEnabled = true;
        GdProgress.Visibility = Visibility.Collapsed;
        _progressLog.Clear();
    }

    public void AddProgressLog(string message)
    {
        _progressLog.Add(message);
        LvProgressLog.UpdateLayout();
    }

    private static void ResizeImage(MagickImage image, SizeSetting sizeSetting, SizeUnit sizeUnit, uint width, uint height)
    {
        if (sizeSetting == SizeSetting.NoResize) return;

        if (sizeUnit == SizeUnit.Percent)
        {
            // Add 0.05 to round up to the nearest integer when converting from percent to pixel
            width = (uint)((image.Width * width / 100.0) + 0.05);
            height = (uint)((image.Height * height / 100.0) + 0.05);
        }

        if (sizeSetting == SizeSetting.ResizeToFill)
        {
            var size = new MagickGeometry(width, height);
            size.IgnoreAspectRatio = true;
            image.Resize(size);
        }
        else if (sizeSetting == SizeSetting.ResizeToWidthAndKeepAspectRatio) image.Resize(width, 0);
        else if (sizeSetting == SizeSetting.ResizeToHeightAndKeepAspectRatio) image.Resize(0, height);
    }

    private async void OnAddImageAppBarButtonClicked(object sender, RoutedEventArgs e)
    {
		var filePicker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        filePicker.ViewMode = PickerViewMode.List;
        filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        foreach (var imageFileFormat in Constants.ImageFileFormats)
        {
            filePicker.FileTypeFilter.Add(imageFileFormat);
        }

        var files = await filePicker.PickMultipleFilesAsync();
        AddImageFiles(files.Select(file => file.Path).ToList());
    }

    private void OnDeleteImageAppBarButtonClicked(object sender, RoutedEventArgs e)
    {
        // Get selected item
        if (LvImages.SelectedItem is not ImageFileViewModel imageFileViewModel) return;

        // Select next or previous item
        var index = _imageFileViewModels.IndexOf(imageFileViewModel);
        var nextIndex = index + 1;
        var previousIndex = index - 1;
        var nextItemExists = nextIndex < _imageFileViewModels.Count;
        var previousItemExists = previousIndex >= 0;
        if (nextItemExists) LvImages.SelectedIndex = nextIndex;
        else if (previousItemExists) LvImages.SelectedIndex = previousIndex;

        // Remove item
        _imageFileViewModels.Remove(imageFileViewModel);
    }

    // Clear all items
    private void OnClearImageAppBarButtonClicked(object sender, RoutedEventArgs e) => _imageFileViewModels.Clear();

    private ImageFileViewModel _selectedImageFileViewModel;
    private void OnImageListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Get selected item
		var imageFileViewModel = LvImages.SelectedItem as ImageFileViewModel;
        _selectedImageFileViewModel = imageFileViewModel;

        // Update app bar buttons state if item is selected
		AbbDelete.IsEnabled = imageFileViewModel != null;

        // Update prefix format preview text box
        UpdatePrefixFormatPreviewTextBox();

        // Update image preview
        if (imageFileViewModel == null)
        {
            ImgPreview.Source = null;
            return;
        }

        var imageFilePath = imageFileViewModel.FilePath;
        var bitmapImage = new BitmapImage() { UriSource = new Uri(imageFilePath) };
        ImgPreview.Source = bitmapImage;
    }

    // Reset image preview zoom factor when new image is selected
    private void OnImagePreviewImageOpened(object sender, RoutedEventArgs e) => ResetImagePreviewZoomFactor();

    // Update prefix format preview text box when prefix text box text changes
    private void OnPrefixTextBoxTextChanged(object sender, TextChangedEventArgs e) => UpdatePrefixFormatPreviewTextBox();

    private void OnFormatComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        var format = GetCurrentOutputFormat();

        // Show or hide quality NumberBox depending on the selected format
        var isQualityAvailable = format == ".jpg" || format == ".jxl" || format == ".webp" || format == ".heif" || format == ".tiff";
        NbQuality.Visibility = isQualityAvailable ? Visibility.Visible : Visibility.Collapsed;

        // Show or hide size settings depending on the selected format
        var isSizeAvailable = format != ".ico";
        SpSizeSettings.Visibility = isSizeAvailable ? Visibility.Visible : Visibility.Collapsed;

        // Update prefix format preview text box
        UpdatePrefixFormatPreviewTextBox();
    }

    private void OnPreviewScrollViewerSizeChanged(object sender, SizeChangedEventArgs e) => ResetImagePreviewZoomFactor();

    private void OnSizeSettingsComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sizeSetting = (SizeSetting)CbxSizeSettings.SelectedIndex;

        // Show or hide size grid and size unit combo box depending on the selected size setting
        if (sizeSetting == SizeSetting.NoResize)
        {
            GdSize.Visibility = Visibility.Collapsed;
            CbxSizeUnit.Visibility = Visibility.Collapsed;
        }
        else
        {
            GdSize.Visibility = Visibility.Visible;
            CbxSizeUnit.Visibility = Visibility.Visible;
        }

        // Enable width and height text boxes
        if(sizeSetting == SizeSetting.ResizeToFill)
        {
            NbxWidth.IsEnabled = true;
            NbxHeight.IsEnabled = true;
        }
        // Enable width text box and disable height text box
        else if (sizeSetting == SizeSetting.ResizeToWidthAndKeepAspectRatio)
        {
            NbxWidth.IsEnabled = true;
            NbxHeight.IsEnabled = false;
        }
        // Enable height text box and disable width text box
        else if (sizeSetting == SizeSetting.ResizeToHeightAndKeepAspectRatio)
        {
            NbxWidth.IsEnabled = false;
            NbxHeight.IsEnabled = true;
        }
    }

    private void OnSizeUnitComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var sizeUnit = (SizeUnit)CbxSizeUnit.SelectedIndex;

        // Update width and height text boxes headers and values depending on the selected size unit
        if (sizeUnit == SizeUnit.Percent)
        {
            NbxWidth.Header = _resourceLoader.GetString("WidthPercent");
            NbxHeight.Header = _resourceLoader.GetString("HeightPercent");

            // Reset values
            NbxWidth.Value = 100;
            NbxHeight.Value = 100;
        }
        else if (sizeUnit == SizeUnit.Pixel) 
        {
            NbxWidth.Header = _resourceLoader.GetString("WidthPixel");
            NbxHeight.Header = _resourceLoader.GetString("HeightPixel");

            // Reset values
            NbxWidth.Value = 0;
            NbxHeight.Value = 0;
        }

    }

    private void OnZoomInAppBarButtonClicked(object sender, RoutedEventArgs e)
    {
        var zoomFactor = SvPreview.ZoomFactor;
        SvPreview.ChangeView(null, null, (float)(zoomFactor + 0.1));
    }

    private void OnZoomOutAppBarButtonClicked(object sender, RoutedEventArgs e)
    {
        var zoomFactor = SvPreview.ZoomFactor;
        SvPreview.ChangeView(null, null, (float)(zoomFactor - 0.1));
    }

    private void OnResetZoomAppBarButtonClicked(object sender, RoutedEventArgs e) => ResetImagePreviewZoomFactor();

    private async void OnConvertButtonClicked(object sender, RoutedEventArgs e) => await ConvertImagesAsync();
}

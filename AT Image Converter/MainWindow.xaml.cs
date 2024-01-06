using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Collections;
using ImageConverterAT.Enums;
using ImageConverterAT.ViewModels;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
namespace ImageConverterAT;

public sealed partial class MainWindow : Window
{
    private readonly bool _isInitialized = false;
    private readonly AdvancedCollectionView _collectionView = [];
    private readonly ObservableCollection<ImageFileViewModel> _imageFileViewModels = [];
    private readonly ObservableCollection<string> _progressLog = [];

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
        _imageFileViewModels.CollectionChanged += OnImageFileViewModelsCollectionChanged;

        // Sort image file view models by file name
        var collectionView = new AdvancedCollectionView(_imageFileViewModels, true);
        collectionView.SortDescriptions.Add(new SortDescription("FileName", SortDirection.Ascending));
        LvImages.ItemsSource = collectionView;
        _collectionView = collectionView;

        LvProgressLog.ItemsSource = _progressLog;

        // Select first item programmatically to trigger selection changed event after initialization
        CbxSizeSettings.SelectedIndex = 0;
        CbxSizeUnit.SelectedIndex = 0;
    }

    private void AddImageFiles(IEnumerable<string> paths)
	{
        // Get previous count to know if we need to select the first item after adding
        var previousCount = _imageFileViewModels.Count;

        // Create view models
        var viewModels = paths.Select(path => new ImageFileViewModel(path));

        // remove duplicates
        viewModels = viewModels.Where(viewModel => !_imageFileViewModels.Any(existingViewModel => existingViewModel.FilePath == viewModel.FilePath));

        // Add view models
        foreach (var viewModel in viewModels) _imageFileViewModels.Add(viewModel);

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
        AddProgressLog("Conversion started");

    var formatName = CbxFormat.SelectedItem as string;
        foreach (ImageFileViewModel viewModel in _collectionView.Cast<ImageFileViewModel>())
        {
            var directoryPath = Path.GetDirectoryName(viewModel.FilePath);
            DispatcherQueue.TryEnqueue(() => LvImages.SelectedItem = viewModel);
            AddProgressLog($"Converting {viewModel.FileName}");

            var fileName = GetSavedFileName(viewModel);
            var filePath = Path.Combine(directoryPath, fileName);

            var image = viewModel.MagickImage;
            var sizeSetting = (SizeSetting)CbxSizeSettings.SelectedIndex;
            var sizeUnit = (SizeUnit)CbxSizeUnit.SelectedIndex;
            if (formatName != "ICO") await Task.Run(() => ResizeImageBySettings(image, sizeSetting, sizeUnit));
            // Convert to ico format if selected
            else
            {
                image.Format = MagickFormat.Ico;

                using var collection = new MagickImageCollection();

                // Define icon sizes (all available size for ico format)
                int[] sizes = [16, 32, 48, 64, 128, 256];

                // Generate icon images with different sizes
                foreach (var size in sizes)
                {
                    image.Resize(size, size);
                    collection.Add(image);
                }

                collection.Write(filePath);
                continue;
            }

            if (formatName == "JPG")
            {
                image.Format = MagickFormat.Jpeg;
                image.Quality = (int)NbQuality.Value;
            }
            else if (formatName == "WEBP")
            {
                image.Format = MagickFormat.WebP;
                image.Quality = (int)NbQuality.Value;
            }
            else if (formatName == "HEIF")
            {
                image.Format = MagickFormat.Heif;
                image.Quality = (int)NbQuality.Value;
            }
            else if (formatName == "TIFF")
            {
                image.Format = MagickFormat.Tiff;
                image.Quality = (int)NbQuality.Value;
            }
            else if (formatName == "PNG") image.Format = MagickFormat.Png;
            else if (formatName == "BMP") image.Format = MagickFormat.Bmp;

            await Task.Run(() => image.Write(filePath));
            AddProgressLog($"{viewModel.FileName} -> {fileName} Complete!");
        }

        AddProgressLog("Conversion complete!");
        AddProgressLog("You can now close this window");
    }

    public void AddProgressLog(string message)
    {
        _progressLog.Add(message);
        LvProgressLog.UpdateLayout();
    }

    private void ResizeImageBySettings(MagickImage image, SizeSetting sizeSetting, SizeUnit sizeUnit)
    {
        if (sizeSetting == SizeSetting.NoResize) return;

        var width = NbxWidth.Value;
        var height = NbxHeight.Value;

        if (sizeUnit == SizeUnit.Percent)
        {
            // Add 0.05 to round up to the nearest integer when converting from percent to pixel
            width = (int)((image.Width * width / 100) + 0.05);
            height = (int)((image.Height * height / 100) + 0.05);
        }

        if (sizeSetting == SizeSetting.ResizeToFill) image.Resize((int)width, (int)height);
        else if (sizeSetting == SizeSetting.ResizeToWidthAndKeepAspectRatio) image.Resize((int)width, 0);
        else if (sizeSetting == SizeSetting.ResizeToHeightAndKeepAspectRatio) image.Resize(0, (int)height);
    }

    // Dispose view models when they are removed from the collection
    private void OnImageFileViewModelsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        var removedItems = e.OldItems?.Cast<ImageFileViewModel>();
        if (removedItems == null) return; // Nothing to dispose of

        foreach (var removedItem in removedItems) removedItem.Dispose();
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
        AddImageFiles(files.Select(file => file.Path));
    }

    private void OnDeleteImageAppBarButtonClicked(object sender, RoutedEventArgs e)
    {
        // Get selected item
        var imageFileViewModel = LvImages.SelectedItem as ImageFileViewModel;
        if (imageFileViewModel == null) return;

        // Select next or previous item
        var advancedCollectionView = LvImages.ItemsSource as AdvancedCollectionView;
        var index = advancedCollectionView.IndexOf(imageFileViewModel);
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
        var isQualityAvailable = format == ".jpg" || format == ".webp" || format == ".heif" || format == ".tiff";
        NbQuality.Visibility = isQualityAvailable ? Visibility.Visible : Visibility.Collapsed;

        // Show or hide size settings depending on the selected format
        var isSizeAvailable = format != "ico";
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
            // Change headers to percent
            NbxWidth.Header = "Width (%)";
            NbxHeight.Header = "Height (%)";

            // Reset values
            NbxWidth.Value = 100;
            NbxHeight.Value = 100;
        }
        else if (sizeUnit == SizeUnit.Pixel) 
        {
            // Change headers to px
            NbxWidth.Header = "Width (px)";
            NbxHeight.Header = "Height (px)";

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

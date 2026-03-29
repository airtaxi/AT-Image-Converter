using ImageConverterAT.Enums;
using ImageConverterAT.ViewModels;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        if (args.Length > 2 && args[1] == "--file-list")
        {
            var fileListPath = args[2];
            if (File.Exists(fileListPath))
            {
                var filePaths = File.ReadAllLines(fileListPath).Where(line => !string.IsNullOrWhiteSpace(line));
                AddImageFiles(filePaths);
                try { File.Delete(fileListPath); } catch { }
            }
        }
        else if (args.Length > 1)
        {
            AddImageFiles(args[1..]); // Skip first argument, which is the executable path
        }

        // Assign data source to list view
        LvImages.ItemsSource = _imageFileViewModels;

        LvProgressLog.ItemsSource = _progressLog;

        // Select first item programmatically to trigger selection changed event after initialization
        CbxSizeSettings.SelectedIndex = 0;
        CbxSizeUnit.SelectedIndex = 0;

        UpdateImageListDependentControls();
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

        UpdateImageListDependentControls();
    }

    private void UpdatePrefixFormatPreviewTextBox()
    {
        var currentFileName = _selectedImageFileViewModel?.FileName;

        if (currentFileName == null)
        {
            TbxPrefixPreview.Text = "";
            return;
        }

        var prefix = TbxPrefix.Text;
        var format = GetCurrentOutputFormat();
        TbxPrefixPreview.Text = GetSavedFileName(_selectedImageFileViewModel, prefix, format);
    }

    private string GetSavedFileName(ImageFileViewModel viewModel, string prefix, string format)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(viewModel.FileName);
        var fileName = prefix + fileNameWithoutExtension + format;
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
        // Check if PDF files are included and Ghostscript is available
        var hasPdfFiles = _imageFileViewModels.Any(viewModel =>
            Path.GetExtension(viewModel.FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase));

        if (hasPdfFiles && !IsGhostscriptInstalled())
        {
            await FrMain.ShowMessageDialogAsync(
                _resourceLoader.GetString("GhostscriptRequiredDialogContent"),
                _resourceLoader.GetString("GhostscriptRequiredDialogTitle"));
            return;
        }

        // TODO: Convert images
        FrPreview.IsEnabled = false;
        SvSettings.IsEnabled = false;
        BtConvert.IsEnabled = false;
        GdProgress.Visibility = Visibility.Visible;
        AddProgressLog(_resourceLoader.GetString("ConversionStarted"));

        var formatName = CbxFormat.SelectedItem as string;
        var rotationSetting = TsRotation.IsOn ? (RotationSetting)CbxRotationSettings.SelectedIndex : (RotationSetting)(-1);
        var sizeSetting = (SizeSetting)CbxSizeSettings.SelectedIndex;
        var sizeUnit = (SizeUnit)CbxSizeUnit.SelectedIndex;
        var width = (uint)NbxWidth.Value;
        var height = (uint)NbxHeight.Value;
        var prefix = TbxPrefix.Text;
        var format = GetCurrentOutputFormat();
        var parallelExecution = TsParallelExecution.IsOn;
        var preserveFileDate = TsPreserveFileDate.IsOn;
        var preserveExif = TsPreserveExif.IsOn;
        var overwriteFile = TsOverwriteFile.IsOn;
        var deleteOriginal = TsDeleteOriginal.IsOn;
        var quality = (uint)NbQuality.Value;
        var outputFolderSetting = GetOutputFolderSetting();
        var customFolderPath = TbxCustomFolderPath.Text;
        var subfolderName = TbxSubfolderName.Text;

        string GetOutputDirectory(string sourceFilePath)
        {
            var sourceDirectory = Path.GetDirectoryName(sourceFilePath);
            if (outputFolderSetting == OutputFolderSetting.SameFolder) return sourceDirectory;
            if (outputFolderSetting == OutputFolderSetting.PhotoFolder)
                return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (outputFolderSetting == OutputFolderSetting.CustomFolder && !string.IsNullOrEmpty(customFolderPath))
                return customFolderPath;
            if (outputFolderSetting == OutputFolderSetting.Subfolder && !string.IsNullOrEmpty(subfolderName))
                return Path.Combine(sourceDirectory, subfolderName);
            return sourceDirectory;
        }

        async Task ConvertSingleImageAsync(ImageFileViewModel viewModel)
        {
            var directoryPath = GetOutputDirectory(viewModel.FilePath);
            Directory.CreateDirectory(directoryPath);
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!parallelExecution) LvImages.SelectedItem = viewModel;
                AddProgressLog(string.Format(_resourceLoader.GetString("ConvertingFile"), viewModel.FileName));
            });

            var fileName = GetSavedFileName(viewModel, prefix, format);
            var filePath = Path.Combine(directoryPath, fileName);

            if (!overwriteFile && File.Exists(filePath))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var counter = 1;
                do
                {
                    fileName = $"{fileNameWithoutExtension} ({counter}){extension}";
                    filePath = Path.Combine(directoryPath, fileName);
                    counter++;
                } while (File.Exists(filePath));
            }

            // Preserve original file dates if needed
            var originalCreationTime = preserveFileDate ? File.GetCreationTime(viewModel.FilePath) : default;
            var originalLastWriteTime = preserveFileDate ? File.GetLastWriteTime(viewModel.FilePath) : default;

            using var image = viewModel.CreateMagickImage();

            if (formatName != "ICO") await Task.Run(() =>
            {
                if (rotationSetting >= 0) RotateImage(image, rotationSetting);
                ResizeImage(image, sizeSetting, sizeUnit, width, height);
            });
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
                    if (rotationSetting >= 0) RotateImage(iconImage, rotationSetting);
                    iconImage.Resize(size, size);
                    collection.Add(iconImage);
                }

                collection.Write(filePath, MagickFormat.Ico);

                if (preserveFileDate)
                {
                    File.SetCreationTime(filePath, originalCreationTime);
                    File.SetLastWriteTime(filePath, originalLastWriteTime);
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (parallelExecution) LvImages.SelectedItem = viewModel;
                    AddProgressLog(string.Format(_resourceLoader.GetString("ConversionFileComplete"), viewModel.FileName, fileName));
                });

                if (deleteOriginal && filePath != viewModel.FilePath)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _imageFileViewModels.Remove(viewModel);
                        UpdateImageListDependentControls();
                        File.Delete(viewModel.FilePath);
                    });
                }
                return;
            }

            // Handle EXIF: strip orientation if manually rotated, strip all if not preserving
            if (!preserveExif) image.Strip();
            else if (rotationSetting > 0) image.SetAttribute("exif:Orientation", "1");

            if (formatName == "JPG")
            {
                image.Format = MagickFormat.Jpeg;
                image.Quality = quality;
            }
            else if (formatName == "JXL")
            {
                image.Format = MagickFormat.Jxl;
                image.Quality = quality;
            }
            else if (formatName == "WEBP")
            {
                image.Format = MagickFormat.WebP;
                image.Quality = quality;
            }
            else if (formatName == "AVIF")
            {
                image.Format = MagickFormat.Avif;
                image.Quality = quality;
            }
            else if (formatName == "TIFF")
            {
                image.Format = MagickFormat.Tiff;
                image.Quality = quality;
            }
            else if (formatName == "PNG") image.Format = MagickFormat.Png;
            else if (formatName == "BMP") image.Format = MagickFormat.Bmp;

            await Task.Run(() => image.Write(filePath));

            if (preserveFileDate)
            {
                File.SetCreationTime(filePath, originalCreationTime);
                File.SetLastWriteTime(filePath, originalLastWriteTime);
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (parallelExecution) LvImages.SelectedItem = viewModel;
                AddProgressLog(string.Format(_resourceLoader.GetString("ConversionFileComplete"), viewModel.FileName, fileName));
            });

            if (deleteOriginal && filePath != viewModel.FilePath)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _imageFileViewModels.Remove(viewModel);
                    UpdateImageListDependentControls();
                    File.Delete(viewModel.FilePath);
                });
            }
        }

        if (parallelExecution) await Parallel.ForEachAsync(_imageFileViewModels.ToList(), async (viewModel, _) => await ConvertSingleImageAsync(viewModel));
        else foreach (var viewModel in _imageFileViewModels.ToList()) await ConvertSingleImageAsync(viewModel);

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

    private void UpdateImageListDependentControls()
    {
        var hasImages = _imageFileViewModels.Count > 0;
        BtDropPlaceholder.Visibility = hasImages ? Visibility.Collapsed : Visibility.Visible;
        BtConvert.IsEnabled = hasImages;
        BtConvert.Content = hasImages
            ? _resourceLoader.GetString("ConvertButtonContent")
            : _resourceLoader.GetString("ConvertButtonNoImagesContent");
    }

    private static bool IsGhostscriptInstalled()
    {
        // Check common Ghostscript installation paths
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (var baseDir in new[] { programFiles, programFilesX86 })
        {
            var gsDir = Path.Combine(baseDir, "gs");
            if (!Directory.Exists(gsDir)) continue;

            // Look for gswin64c.exe or gswin32c.exe in any version subdirectory
            try
            {
                var found = Directory.EnumerateFiles(gsDir, "gswin*c.exe", SearchOption.AllDirectories).Any();
                if (found) return true;
            }
            catch { }
        }

        // Check if gswin64c.exe or gswin32c.exe is on PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir, "gswin64c.exe")) ||
                    File.Exists(Path.Combine(dir, "gswin32c.exe")))
                    return true;
            }
            catch { }
        }

        return false;
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
        AddImageFiles([.. files.Select(file => file.Path)]);
    }

    private void OnAddImageAppBarButtonKeyboardAcceleratorInvoked(object sender, KeyboardAcceleratorInvokedEventArgs e)
    {
        e.Handled = true;

        OnAddImageAppBarButtonClicked(sender, null);
    }

    private void OnDropPlaceholderButtonClicked(object sender, RoutedEventArgs e) => OnAddImageAppBarButtonClicked(sender, null);

    private void OnDropPlaceholderButtonDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
    }

    private async void OnDropPlaceholderButtonDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems)) return;

        var items = await e.DataView.GetStorageItemsAsync();
        var supportedExtensions = new HashSet<string>(Constants.ImageFileFormats, StringComparer.OrdinalIgnoreCase);

        var filePaths = items
            .OfType<Windows.Storage.StorageFile>()
            .Where(file => supportedExtensions.Contains(file.FileType))
            .Select(file => file.Path)
            .ToList();

        if (filePaths.Count > 0) AddImageFiles(filePaths);
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
        UpdateImageListDependentControls();
    }

    // Clear all items
    private void OnClearImageAppBarButtonClicked(object sender, RoutedEventArgs e)
    {
        _imageFileViewModels.Clear();
        UpdateImageListDependentControls();
    }

    private ImageFileViewModel _selectedImageFileViewModel;
    private void OnImageListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Get selected item
        var imageFileViewModel = LvImages.SelectedItem as ImageFileViewModel;
        _selectedImageFileViewModel = imageFileViewModel;

        // Update app bar buttons state if item is selected
        AbbDelete.IsEnabled = imageFileViewModel != null;
        BtOpenSameFolder.IsEnabled = imageFileViewModel != null;
        BtOpenSubfolder.IsEnabled = imageFileViewModel != null;

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
        var isQualityAvailable = format == ".jpg" || format == ".jxl" || format == ".webp" || format == ".avif" || format == ".tiff";
        NbQuality.Visibility = isQualityAvailable ? Visibility.Visible : Visibility.Collapsed;

        // Show or hide size settings depending on the selected format
        var isSizeAvailable = format != ".ico";
        SpSizeSettings.Visibility = isSizeAvailable ? Visibility.Visible : Visibility.Collapsed;

        // Update prefix format preview text box
        UpdatePrefixFormatPreviewTextBox();
    }

    private void OnPreviewScrollViewerSizeChanged(object sender, SizeChangedEventArgs e) => ResetImagePreviewZoomFactor();

    private void OnRotationToggleSwitchToggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        CbxRotationSettings.Visibility = TsRotation.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void RotateImage(MagickImage image, RotationSetting rotationSetting)
    {
        if (rotationSetting == RotationSetting.AutoRotateByExif) image.AutoOrient();
        else if (rotationSetting == RotationSetting.RotateClockwise90) image.Rotate(90);
        else if (rotationSetting == RotationSetting.RotateCounterClockwise90) image.Rotate(270);
        else if (rotationSetting == RotationSetting.Rotate180) image.Rotate(180);
    }

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
        if (sizeSetting == SizeSetting.ResizeToFill)
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

    private OutputFolderSetting GetOutputFolderSetting()
    {
        if (RbPhotoFolder.IsChecked == true) return OutputFolderSetting.PhotoFolder;
        if (RbCustomFolder.IsChecked == true) return OutputFolderSetting.CustomFolder;
        if (RbSubfolder.IsChecked == true) return OutputFolderSetting.Subfolder;
        return OutputFolderSetting.SameFolder;
    }

    private void OnOpenSameFolderButtonClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedImageFileViewModel == null) return;
        var directoryPath = Path.GetDirectoryName(_selectedImageFileViewModel.FilePath);
        Process.Start(new ProcessStartInfo(directoryPath) { UseShellExecute = true });
    }

    private void OnOpenPhotoFolderButtonClicked(object sender, RoutedEventArgs e)
    {
        var photoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        Process.Start(new ProcessStartInfo(photoFolder) { UseShellExecute = true });
    }

    private void OnOpenCustomFolderButtonClicked(object sender, RoutedEventArgs e)
    {
        var path = TbxCustomFolderPath.Text;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OnOpenSubfolderButtonClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedImageFileViewModel == null) return;
        var sourceDirectory = Path.GetDirectoryName(_selectedImageFileViewModel.FilePath);
        var subfolderName = TbxSubfolderName.Text;
        if (string.IsNullOrEmpty(subfolderName)) return;
        var subfolderPath = Path.Combine(sourceDirectory, subfolderName);
        if (!Directory.Exists(subfolderPath)) return;
        Process.Start(new ProcessStartInfo(subfolderPath) { UseShellExecute = true });
    }

    private async void OnBrowseCustomFolderButtonClicked(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        folderPicker.FileTypeFilter.Add("*");

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder == null) return;

        TbxCustomFolderPath.Text = folder.Path;
        BtOpenCustomFolder.IsEnabled = true;
        RbCustomFolder.IsChecked = true;
    }

    private async void OnConvertButtonClicked(object sender, RoutedEventArgs e) => await ConvertImagesAsync();
}

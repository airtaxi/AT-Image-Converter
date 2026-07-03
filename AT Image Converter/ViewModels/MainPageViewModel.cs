using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImageConverterAT.Enums;
using ImageConverterAT.Models;
using ImageConverterAT.Services;
using ImageMagick;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Globalization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.System;
using Windows.UI;

namespace ImageConverterAT.ViewModels;

public sealed partial class MainPageViewModel : ObservableObject, IRecipient<AddImageFilesMessage>, IRecipient<FolderSelectedMessage>
{
    private static readonly Uri s_gitHubRepositoryPageAddress = new("https://github.com/airtaxi/AT-Image-Converter");
    private static Color CreateDefaultCropBackgroundColor() => Color.FromArgb(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    private static Color CreateDefaultTransparentColor() => Color.FromArgb(byte.MaxValue, 0, 0, 0);

    private readonly ResourceLoader _resourceLoader = new();
    private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

    public ObservableCollection<ImageFileViewModel> ImageFileViewModels { get; } = [];
    public ObservableCollection<string> ProgressLog { get; } = [];

    public SolidColorBrush CropBackgroundColorBrush => new(CropBackgroundColor);
    public SolidColorBrush TransparentColorBrush => new(TransparentColor);
    public string ConvertButtonContent => HasImages ? _resourceLoader.GetString("ConvertButtonContent") : _resourceLoader.GetString("ConvertButtonNoImagesContent");
    public bool IsSameFolderChecked => OutputFolderSetting == OutputFolderSetting.SameFolder;
    public bool IsPhotoFolderChecked => OutputFolderSetting == OutputFolderSetting.PhotoFolder;
    public bool IsCustomFolderChecked => OutputFolderSetting == OutputFolderSetting.CustomFolder;
    public bool IsSubfolderChecked => OutputFolderSetting == OutputFolderSetting.Subfolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeleteEnabled))]
    [NotifyPropertyChangedFor(nameof(IsOpenSameFolderEnabled))]
    [NotifyPropertyChangedFor(nameof(IsOpenSubfolderEnabled))]
    [NotifyPropertyChangedFor(nameof(IsNoPreviewVisible))]
    [NotifyPropertyChangedFor(nameof(IsDropPlaceholderVisible))]
    [NotifyPropertyChangedFor(nameof(IsConvertEnabled))]
    [NotifyPropertyChangedFor(nameof(HasImages))]
    public partial ImageFileViewModel SelectedImageFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQualityAvailable))]
    [NotifyPropertyChangedFor(nameof(IsSizeAvailable))]
    [NotifyPropertyChangedFor(nameof(OutputFormatSupportsAlpha))]
    [NotifyPropertyChangedFor(nameof(CropBackgroundColorDisplayValue))]
    [NotifyPropertyChangedFor(nameof(TransparentColorDisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsTransparentColorVisible))]
    [NotifyPropertyChangedFor(nameof(CurrentOutputFormat))]
    public partial int FormatIndex { get; set; }

    [ObservableProperty]
    public partial double Quality { get; set; } = 80;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRotationSettingsVisible))]
    public partial bool RotationEnabled { get; set; } = true;

    [ObservableProperty]
    public partial int RotationIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSizeGridVisible))]
    [NotifyPropertyChangedFor(nameof(IsSizeUnitVisible))]
    [NotifyPropertyChangedFor(nameof(IsResizeInterpolationVisible))]
    [NotifyPropertyChangedFor(nameof(IsCropVisible))]
    [NotifyPropertyChangedFor(nameof(IsWidthEnabled))]
    [NotifyPropertyChangedFor(nameof(IsHeightEnabled))]
    [NotifyPropertyChangedFor(nameof(WidthMinimum))]
    [NotifyPropertyChangedFor(nameof(HeightMinimum))]
    public partial int SizeSettingIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WidthHeader))]
    [NotifyPropertyChangedFor(nameof(HeightHeader))]
    public partial int SizeUnitIndex { get; set; }

    [ObservableProperty]
    public partial int ResizeInterpolationIndex { get; set; }

    [ObservableProperty]
    public partial double Width { get; set; }

    [ObservableProperty]
    public partial double Height { get; set; }

    [ObservableProperty]
    public partial int CropHorizontalAnchorIndex { get; set; } = 1;

    [ObservableProperty]
    public partial int CropVerticalAnchorIndex { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CropBackgroundColorDisplayValue))]
    [NotifyPropertyChangedFor(nameof(CropBackgroundColorBrush))]
    public partial Color CropBackgroundColor { get; set; } = CreateDefaultCropBackgroundColor();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TransparentColorDisplayValue))]
    [NotifyPropertyChangedFor(nameof(TransparentColorBrush))]
    public partial Color TransparentColor { get; set; } = CreateDefaultTransparentColor();

    [ObservableProperty]
    public partial string Prefix { get; set; } = "ATIC_";

    [ObservableProperty]
    public partial string PrefixPreview { get; set; } = "";

    [ObservableProperty]
    public partial bool OverwriteFile { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSameFolderChecked))]
    [NotifyPropertyChangedFor(nameof(IsPhotoFolderChecked))]
    [NotifyPropertyChangedFor(nameof(IsCustomFolderChecked))]
    [NotifyPropertyChangedFor(nameof(IsSubfolderChecked))]
    public partial OutputFolderSetting OutputFolderSetting { get; set; } = OutputFolderSetting.SameFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpenCustomFolderEnabled))]
    public partial string CustomFolderPath { get; set; } = "";

    [ObservableProperty]
    public partial string SubfolderName { get; set; } = "output";

    [ObservableProperty]
    public partial bool ParallelExecution { get; set; } = true;

    [ObservableProperty]
    public partial bool PreserveFileDate { get; set; } = true;

    [ObservableProperty]
    public partial bool PreserveExif { get; set; } = true;

    [ObservableProperty]
    public partial bool PreserveAnimation { get; set; } = true;

    [ObservableProperty]
    public partial bool DeleteOriginal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConvertEnabled))]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    public partial bool IsConverting { get; set; }

    [ObservableProperty]
    public partial bool IsEnglishLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsKoreanLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsJapaneseLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsSimplifiedChineseLanguageChecked { get; set; }

    [ObservableProperty]
    public partial bool IsTraditionalChineseLanguageChecked { get; set; }

    public bool HasImages => ImageFileViewModels.Count > 0;
    public bool IsConvertEnabled => HasImages && !IsConverting;
    public bool IsDeleteEnabled => SelectedImageFile != null;
    public bool IsOpenSameFolderEnabled => SelectedImageFile != null;
    public bool IsOpenSubfolderEnabled => SelectedImageFile != null;
    public bool IsOpenCustomFolderEnabled => !string.IsNullOrEmpty(CustomFolderPath);
    public bool IsQualityAvailable => CurrentOutputFormat is ".jpg" or ".jxl" or ".webp" or ".avif" or ".tiff";
    public bool IsSizeAvailable => CurrentOutputFormat != ".ico";
    public bool IsSizeGridVisible => SizeSetting != SizeSetting.NoResize;
    public bool IsSizeUnitVisible => SizeSetting != SizeSetting.NoResize;
    public bool IsResizeInterpolationVisible => SizeSetting is not SizeSetting.NoResize and not SizeSetting.CropToSize;
    public bool IsCropVisible => SizeSetting == SizeSetting.CropToSize;
    public bool HasAlphaSupportingInputImages => ImageFileViewModels.Any(viewModel => Constants.AlphaSupportingInputExtensions.Contains(Path.GetExtension(viewModel.FilePath)));
    public bool IsTransparentColorVisible => HasAlphaSupportingInputImages && !OutputFormatSupportsAlpha;
    public bool IsWidthEnabled => SizeSetting is SizeSetting.ResizeToFill or SizeSetting.CropToSize or SizeSetting.ResizeToWidthAndKeepAspectRatio;
    public bool IsHeightEnabled => SizeSetting is SizeSetting.ResizeToFill or SizeSetting.CropToSize or SizeSetting.ResizeToHeightAndKeepAspectRatio;
    public bool IsRotationSettingsVisible => RotationEnabled;
    public bool IsDropPlaceholderVisible => !HasImages;
    public bool IsProgressVisible => IsConverting;
    public bool IsNoPreviewVisible => !HasImages || SelectedImageFile == null;

    public string CurrentOutputFormat => "." + CurrentOutputFormatName.ToLower();
    private string CurrentOutputFormatName => FormatIndex switch
    {
        0 => "JPG",
        1 => "JXL",
        2 => "PNG",
        3 => "BMP",
        4 => "WEBP",
        5 => "AVIF",
        6 => "ICO",
        7 => "TIFF",
        _ => "JPG"
    };

    public bool OutputFormatSupportsAlpha => CurrentOutputFormat is ".png" or ".jxl" or ".webp" or ".avif" or ".ico" or ".tiff";

    public string CropBackgroundColorDisplayValue => GetDisplayedColorValue(CropBackgroundColor, OutputFormatSupportsAlpha);
    public string TransparentColorDisplayValue => GetDisplayedColorValue(TransparentColor, OutputFormatSupportsAlpha);

    public string WidthHeader => SizeUnit == SizeUnit.Percent ? _resourceLoader.GetString("WidthPercent") : _resourceLoader.GetString("WidthPixel");
    public string HeightHeader => SizeUnit == SizeUnit.Percent ? _resourceLoader.GetString("HeightPercent") : _resourceLoader.GetString("HeightPixel");

    public double WidthMinimum => SizeSetting == SizeSetting.CropToSize ? 1 : 0;
    public double HeightMinimum => SizeSetting == SizeSetting.CropToSize ? 1 : 0;

    private SizeSetting SizeSetting => (SizeSetting)SizeSettingIndex;
    private SizeUnit SizeUnit => (SizeUnit)SizeUnitIndex;

    public MainPageViewModel()
    {
        WeakReferenceMessenger.Default.Register<AddImageFilesMessage>(this);
        WeakReferenceMessenger.Default.Register<FolderSelectedMessage>(this);
        LoadSettings();
        UpdateLanguageMenuFlyoutItems();
    }

    public void Receive(AddImageFilesMessage message) => AddImageFiles(message.FilePaths);
    public void Receive(FolderSelectedMessage message) => OnFolderSelected(message.FolderPath);

    partial void OnFormatIndexChanged(int value)
    {
        if (CropBackgroundColor.A < byte.MaxValue && !OutputFormatSupportsAlpha) CropBackgroundColor = CreateOpaqueColor(CropBackgroundColor);
        if (TransparentColor.A < byte.MaxValue && !OutputFormatSupportsAlpha) TransparentColor = CreateOpaqueColor(TransparentColor);
        UpdatePrefixFormatPreview();
    }

    partial void OnSelectedImageFileChanged(ImageFileViewModel value) => UpdatePrefixFormatPreview();

    partial void OnSizeSettingIndexChanged(int value)
    {
        if (SizeSetting == SizeSetting.CropToSize)
        {
            if (Width < 1) Width = 1;
            if (Height < 1) Height = 1;
        }
    }

    partial void OnSizeUnitIndexChanged(int value)
    {
        if (SizeUnit == SizeUnit.Percent)
        {
            Width = 100;
            Height = 100;
        }
        else
        {
            Width = 0;
            Height = 0;
        }
    }

    partial void OnPrefixChanged(string value) => UpdatePrefixFormatPreview();

    private void UpdateImageListDependentProperties()
    {
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(IsConvertEnabled));
        OnPropertyChanged(nameof(IsDropPlaceholderVisible));
        OnPropertyChanged(nameof(IsNoPreviewVisible));
        OnPropertyChanged(nameof(ConvertButtonContent));
        OnPropertyChanged(nameof(HasAlphaSupportingInputImages));
        OnPropertyChanged(nameof(IsTransparentColorVisible));
    }

    private void AddImageFiles(IEnumerable<string> paths)
    {
        var previousCount = ImageFileViewModels.Count;
        var existingPaths = new HashSet<string>(ImageFileViewModels.Select(viewModel => viewModel.FilePath));

        var newViewModels = paths
            .Where(existingPaths.Add)
            .Select(path => new ImageFileViewModel(path))
            .ToList();

        if (newViewModels.Count == 0) return;

        foreach (var viewModel in newViewModels) ImageFileViewModels.Add(viewModel);

        var sorted = ImageFileViewModels.OrderBy(viewModel => viewModel.FileName, StringComparer.Ordinal).ToList();
        ImageFileViewModels.Clear();
        foreach (var viewModel in sorted) ImageFileViewModels.Add(viewModel);

        if (previousCount == 0)
        {
            SelectedImageFile = ImageFileViewModels.FirstOrDefault();
            UpdatePrefixFormatPreview();
        }

        UpdateImageListDependentProperties();
    }

    private void UpdatePrefixFormatPreview()
    {
        var selectedImageFileViewModel = SelectedImageFile;
        if (selectedImageFileViewModel == null)
        {
            PrefixPreview = "";
            return;
        }

        PrefixPreview = GetSavedFileName(selectedImageFileViewModel, Prefix, CurrentOutputFormat);
    }

    private static string GetSavedFileName(ImageFileViewModel viewModel, string prefix, string format)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(viewModel.FileName);
        return prefix + fileNameWithoutExtension + format;
    }

    private static uint? GetPositiveDimensionOrNull(uint dimension) => dimension > 0 ? dimension : null;

    private static uint ScaleDimensionByPercent(uint originalDimension, uint percent)
    {
        if (originalDimension == 0 || percent == 0) return 0;
        return (uint)Math.Ceiling(originalDimension * percent / 100d);
    }

    private static bool DoesOutputFormatSupportAlpha(string outputFormat) => outputFormat is ".png" or ".jxl" or ".webp" or ".avif" or ".ico" or ".tiff";

    private static Color CreateOpaqueColor(Color color) => Color.FromArgb(byte.MaxValue, color.R, color.G, color.B);

    private static string ConvertColorToSettingValue(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static string GetDisplayedColorValue(Color color, bool includeAlpha) => includeAlpha ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}" : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool TryParseHexadecimalByte(string value, out byte component) => byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out component);

    private static Color ConvertSettingValueToColor(string colorValue)
    {
        if (string.IsNullOrWhiteSpace(colorValue)) return CreateDefaultCropBackgroundColor();

        var normalizedColorValue = colorValue.Trim().TrimStart('#');
        if (normalizedColorValue.Length == 6 && TryParseHexadecimalByte(normalizedColorValue[0..2], out var redComponent) && TryParseHexadecimalByte(normalizedColorValue[2..4], out var greenComponent) && TryParseHexadecimalByte(normalizedColorValue[4..6], out var blueComponent))
            return Color.FromArgb(byte.MaxValue, redComponent, greenComponent, blueComponent);

        if (normalizedColorValue.Length == 8 && TryParseHexadecimalByte(normalizedColorValue[0..2], out var alphaComponent) && TryParseHexadecimalByte(normalizedColorValue[2..4], out var redComponentWithAlpha) && TryParseHexadecimalByte(normalizedColorValue[4..6], out var greenComponentWithAlpha) && TryParseHexadecimalByte(normalizedColorValue[6..8], out var blueComponentWithAlpha))
            return Color.FromArgb(alphaComponent, redComponentWithAlpha, greenComponentWithAlpha, blueComponentWithAlpha);

        return CreateDefaultCropBackgroundColor();
    }

    private static MagickColor CreateMagickColor(Color color) => MagickColor.FromRgba(color.R, color.G, color.B, color.A);

    private static Gravity GetCropGravity(HorizontalCropAnchor horizontalCropAnchor, VerticalCropAnchor verticalCropAnchor)
    {
        if (verticalCropAnchor == VerticalCropAnchor.Top && horizontalCropAnchor == HorizontalCropAnchor.Left) return Gravity.Northwest;
        if (verticalCropAnchor == VerticalCropAnchor.Top && horizontalCropAnchor == HorizontalCropAnchor.Center) return Gravity.North;
        if (verticalCropAnchor == VerticalCropAnchor.Top) return Gravity.Northeast;
        if (verticalCropAnchor == VerticalCropAnchor.Center && horizontalCropAnchor == HorizontalCropAnchor.Left) return Gravity.West;
        if (verticalCropAnchor == VerticalCropAnchor.Center && horizontalCropAnchor == HorizontalCropAnchor.Center) return Gravity.Center;
        if (verticalCropAnchor == VerticalCropAnchor.Center) return Gravity.East;
        if (horizontalCropAnchor == HorizontalCropAnchor.Left) return Gravity.Southwest;
        if (horizontalCropAnchor == HorizontalCropAnchor.Center) return Gravity.South;
        return Gravity.Southeast;
    }

    private static int GetHorizontalCropOffset(uint imageWidth, uint cropWidth, HorizontalCropAnchor horizontalCropAnchor)
    {
        if (horizontalCropAnchor == HorizontalCropAnchor.Right) return (int)(imageWidth - cropWidth);
        if (horizontalCropAnchor == HorizontalCropAnchor.Center) return (int)((imageWidth - cropWidth) / 2);
        return 0;
    }

    private static int GetVerticalCropOffset(uint imageHeight, uint cropHeight, VerticalCropAnchor verticalCropAnchor)
    {
        if (verticalCropAnchor == VerticalCropAnchor.Bottom) return (int)(imageHeight - cropHeight);
        if (verticalCropAnchor == VerticalCropAnchor.Center) return (int)((imageHeight - cropHeight) / 2);
        return 0;
    }

    private static (uint Width, uint Height) GetTargetDimensions(MagickImage image, SizeUnit sizeUnit, uint width, uint height)
    {
        if (sizeUnit == SizeUnit.Pixel) return (width, height);

        var targetWidth = ScaleDimensionByPercent(image.Width, width);
        var targetHeight = ScaleDimensionByPercent(image.Height, height);
        return (targetWidth, targetHeight);
    }

    private static void CropImageToTargetSize(MagickImage image, uint targetWidth, uint targetHeight, HorizontalCropAnchor horizontalCropAnchor, VerticalCropAnchor verticalCropAnchor, MagickColor cropBackgroundColor)
    {
        var cropWidth = Math.Min(image.Width, targetWidth);
        var cropHeight = Math.Min(image.Height, targetHeight);
        if (cropWidth != image.Width || cropHeight != image.Height)
        {
            var horizontalCropOffset = GetHorizontalCropOffset(image.Width, cropWidth, horizontalCropAnchor);
            var verticalCropOffset = GetVerticalCropOffset(image.Height, cropHeight, verticalCropAnchor);
            image.Crop(new MagickGeometry(horizontalCropOffset, verticalCropOffset, cropWidth, cropHeight));
            image.ResetPage();
        }

        if (image.Width == targetWidth && image.Height == targetHeight) return;
        image.Extent(targetWidth, targetHeight, GetCropGravity(horizontalCropAnchor, verticalCropAnchor), cropBackgroundColor);
    }

    private static void CleanTransparentPixels(MagickImage image, MagickColor backgroundColor, bool preserveAlpha)
    {
        if (!image.HasAlpha) return;
        using var background = new MagickImage(backgroundColor, image.Width, image.Height);
        background.Composite(image, CompositeOperator.Over);
        if (preserveAlpha) background.Composite(image, CompositeOperator.CopyAlpha);
        else background.Alpha(AlphaOption.Off);
        image.CopyPixels(background);
        if (!preserveAlpha) image.Alpha(AlphaOption.Off);
    }

    private static (uint? RasterizedWidth, uint? RasterizedHeight) GetSvgRasterizedDimensions(ImageFileViewModel imageFileViewModel, SizeSetting sizeSetting, SizeUnit sizeUnit, uint width, uint height)
    {
        if (!imageFileViewModel.IsSvgFile) return (null, null);
        if (sizeSetting == SizeSetting.NoResize) return (null, null);
        if (sizeSetting == SizeSetting.CropToSize) return (null, null);

        if (sizeUnit == SizeUnit.Pixel)
        {
            if (sizeSetting == SizeSetting.ResizeToFill) return (GetPositiveDimensionOrNull(width), GetPositiveDimensionOrNull(height));
            if (sizeSetting == SizeSetting.ResizeToWidthAndKeepAspectRatio) return (GetPositiveDimensionOrNull(width), null);
            if (sizeSetting == SizeSetting.ResizeToHeightAndKeepAspectRatio) return (null, GetPositiveDimensionOrNull(height));
            return (null, null);
        }

        using var originalSvgImage = imageFileViewModel.CreateMagickImage();
        var originalWidth = originalSvgImage.Width;
        var originalHeight = originalSvgImage.Height;
        if (sizeSetting == SizeSetting.ResizeToFill) return (GetPositiveDimensionOrNull(ScaleDimensionByPercent(originalWidth, width)), GetPositiveDimensionOrNull(ScaleDimensionByPercent(originalHeight, height)));
        if (sizeSetting == SizeSetting.ResizeToWidthAndKeepAspectRatio) return (GetPositiveDimensionOrNull(ScaleDimensionByPercent(originalWidth, width)), null);
        if (sizeSetting == SizeSetting.ResizeToHeightAndKeepAspectRatio) return (null, GetPositiveDimensionOrNull(ScaleDimensionByPercent(originalHeight, height)));
        return (null, null);
    }

    private static MagickImage CreateConversionMagickImage(ImageFileViewModel imageFileViewModel, SizeSetting sizeSetting, SizeUnit sizeUnit, uint width, uint height)
    {
        var (rasterizedWidth, rasterizedHeight) = GetSvgRasterizedDimensions(imageFileViewModel, sizeSetting, sizeUnit, width, height);
        return imageFileViewModel.CreateMagickImage(rasterizedWidth, rasterizedHeight);
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
                var found = Directory.EnumerateFiles(gsDir, "gswin*c.exe", SearchOption.AllDirectories).Any();
                if (found) return true;
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

    private static FilterType? GetResizeFilterType(ResizeInterpolationSetting resizeInterpolationSetting)
    {
        if (resizeInterpolationSetting == ResizeInterpolationSetting.NoInterpolation) return FilterType.Point;
        if (resizeInterpolationSetting == ResizeInterpolationSetting.Box) return FilterType.Box;
        if (resizeInterpolationSetting == ResizeInterpolationSetting.Triangle) return FilterType.Triangle;
        if (resizeInterpolationSetting == ResizeInterpolationSetting.Cubic) return FilterType.Cubic;
        if (resizeInterpolationSetting == ResizeInterpolationSetting.Lanczos) return FilterType.Lanczos;
        return null;
    }

    private static void ApplyResize(MagickImage image, MagickGeometry geometry, ResizeInterpolationSetting resizeInterpolationSetting)
    {
        var filterType = GetResizeFilterType(resizeInterpolationSetting);
        if (filterType.HasValue) image.Resize(geometry, filterType.Value);
        else image.Resize(geometry);
    }

    private static void ApplyResize(MagickImage image, uint width, uint height, ResizeInterpolationSetting resizeInterpolationSetting)
    {
        var filterType = GetResizeFilterType(resizeInterpolationSetting);
        if (filterType.HasValue) image.Resize(width, height, filterType.Value);
        else image.Resize(width, height);
    }

    private static void ResizeImage(MagickImage image, SizeSetting sizeSetting, SizeUnit sizeUnit, ResizeInterpolationSetting resizeInterpolationSetting, uint width, uint height, HorizontalCropAnchor horizontalCropAnchor, VerticalCropAnchor verticalCropAnchor, MagickColor cropBackgroundColor)
    {
        if (sizeSetting == SizeSetting.NoResize) return;

        var (targetWidth, targetHeight) = GetTargetDimensions(image, sizeUnit, width, height);

        if (sizeSetting == SizeSetting.ResizeToFill)
        {
            var size = new MagickGeometry(targetWidth, targetHeight);
            size.IgnoreAspectRatio = true;
            ApplyResize(image, size, resizeInterpolationSetting);
        }
        else if (sizeSetting == SizeSetting.ResizeToWidthAndKeepAspectRatio) ApplyResize(image, targetWidth, 0, resizeInterpolationSetting);
        else if (sizeSetting == SizeSetting.ResizeToHeightAndKeepAspectRatio) ApplyResize(image, 0, targetHeight, resizeInterpolationSetting);
        else if (sizeSetting == SizeSetting.CropToSize) CropImageToTargetSize(image, targetWidth, targetHeight, horizontalCropAnchor, verticalCropAnchor, cropBackgroundColor);
    }

    private static void RotateImage(MagickImage image, RotationSetting rotationSetting)
    {
        if (rotationSetting == RotationSetting.AutoRotateByExif) image.AutoOrient();
        else if (rotationSetting == RotationSetting.RotateClockwise90) image.Rotate(90);
        else if (rotationSetting == RotationSetting.RotateCounterClockwise90) image.Rotate(270);
        else if (rotationSetting == RotationSetting.Rotate180) image.Rotate(180);
    }

    private void UpdateLanguageMenuFlyoutItems()
    {
        var currentLanguageTag = GetCurrentLanguageTag();
        IsEnglishLanguageChecked = LanguageTagsMatch(currentLanguageTag, "en-US");
        IsKoreanLanguageChecked = LanguageTagsMatch(currentLanguageTag, "ko-KR");
        IsJapaneseLanguageChecked = LanguageTagsMatch(currentLanguageTag, "ja-JP");
        IsSimplifiedChineseLanguageChecked = LanguageTagsMatch(currentLanguageTag, "zh-Hans");
        IsTraditionalChineseLanguageChecked = LanguageTagsMatch(currentLanguageTag, "zh-Hant");
    }

    private static string GetCurrentLanguageTag()
    {
        var primaryLanguageOverride = ApplicationLanguages.PrimaryLanguageOverride;
        if (!string.IsNullOrWhiteSpace(primaryLanguageOverride)) return primaryLanguageOverride;
        return ApplicationLanguages.Languages[0] ?? "en-US";
    }

    private static bool LanguageTagsMatch(string currentLanguageTag, string supportedLanguageTag)
    {
        var normalizedCurrentLanguageTag = NormalizeLanguageTagForMenu(currentLanguageTag);
        var normalizedSupportedLanguageTag = NormalizeLanguageTagForMenu(supportedLanguageTag);
        if (string.Equals(normalizedCurrentLanguageTag, normalizedSupportedLanguageTag, StringComparison.OrdinalIgnoreCase)) return true;
        if (normalizedSupportedLanguageTag.StartsWith("zh-", StringComparison.OrdinalIgnoreCase)) return false;

        var currentLanguagePrefix = normalizedCurrentLanguageTag.Split('-')[0];
        var supportedLanguagePrefix = normalizedSupportedLanguageTag.Split('-')[0];
        return string.Equals(currentLanguagePrefix, supportedLanguagePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguageTagForMenu(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)) return "en-US";

        var languageTagSegments = languageTag.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (languageTagSegments.Length == 0) return "en-US";
        if (!string.Equals(languageTagSegments[0], "zh", StringComparison.OrdinalIgnoreCase)) return languageTag;

        var isTraditionalChinese = languageTagSegments.Any(segment => string.Equals(segment, "Hant", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "TW", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "HK", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "MO", StringComparison.OrdinalIgnoreCase));
        if (isTraditionalChinese) return "zh-Hant";

        var isSimplifiedChinese = languageTagSegments.Any(segment => string.Equals(segment, "Hans", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "CN", StringComparison.OrdinalIgnoreCase) || string.Equals(segment, "SG", StringComparison.OrdinalIgnoreCase));
        if (isSimplifiedChinese) return "zh-Hans";

        return "zh-Hans";
    }

    [RelayCommand]
    private void AddImages() => WeakReferenceMessenger.Default.Send(ShowFileOpenPickerMessage.Instance);

    [RelayCommand]
    private void DeleteImage()
    {
        if (SelectedImageFile == null) return;

        var index = ImageFileViewModels.IndexOf(SelectedImageFile);
        var nextIndex = index + 1;
        var previousIndex = index - 1;
        var nextItemExists = nextIndex < ImageFileViewModels.Count;
        var previousItemExists = previousIndex >= 0;

        // Cache the next selected image file before removing the current one
        ImageFileViewModel nextSelectedImageFile = null;
        if (nextItemExists) nextSelectedImageFile = ImageFileViewModels[nextIndex];
        else if (previousItemExists) nextSelectedImageFile = ImageFileViewModels[previousIndex];

        // Delete the selected image file from the collection
        ImageFileViewModels.Remove(SelectedImageFile);

        // Reset the selected image file to the next or previous item, if available
        SelectedImageFile = nextSelectedImageFile;

        UpdateImageListDependentProperties();
    }

    [RelayCommand]
    private void ClearImages()
    {
        ImageFileViewModels.Clear();
        SelectedImageFile = null;
        UpdateImageListDependentProperties();
    }

    [RelayCommand]
    private async Task ConvertAsync() => await ConvertImagesAsync();

    [RelayCommand]
    private void SaveDefaults() => SaveSettings();

    [RelayCommand]
    private void ResetDefaults() => ResetSettings();

    [RelayCommand]
    private void BrowseCustomFolder() => WeakReferenceMessenger.Default.Send(ShowFolderPickerMessage.Instance);

    [RelayCommand]
    private void SelectOutputFolder(string outputFolderName) => OutputFolderSetting = outputFolderName switch
    {
        "SameFolder" => OutputFolderSetting.SameFolder,
        "PhotoFolder" => OutputFolderSetting.PhotoFolder,
        "CustomFolder" => OutputFolderSetting.CustomFolder,
        "Subfolder" => OutputFolderSetting.Subfolder,
        _ => OutputFolderSetting.SameFolder
    };

    [RelayCommand]
    private void OpenSameFolder()
    {
        if (SelectedImageFile == null) return;
        var directoryPath = Path.GetDirectoryName(SelectedImageFile.FilePath);
        Process.Start(new ProcessStartInfo(directoryPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenPhotoFolder()
    {
        var photoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        Process.Start(new ProcessStartInfo(photoFolder) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenCustomFolder()
    {
        if (string.IsNullOrEmpty(CustomFolderPath) || !Directory.Exists(CustomFolderPath)) return;
        Process.Start(new ProcessStartInfo(CustomFolderPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenSubfolder()
    {
        if (SelectedImageFile == null) return;
        var sourceDirectory = Path.GetDirectoryName(SelectedImageFile.FilePath);
        if (string.IsNullOrEmpty(SubfolderName)) return;
        var subfolderPath = Path.Combine(sourceDirectory, SubfolderName);
        if (!Directory.Exists(subfolderPath)) return;
        Process.Start(new ProcessStartInfo(subfolderPath) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        MainWindow.Instance.ShowLoading(_resourceLoader.GetString("UpdateCheckLoading"));
        try
        {
            var availableUpdateCount = await StoreUpdateService.GetAvailableUpdateCountAsync();
            MainWindow.Instance.HideLoading();
            if (availableUpdateCount <= 0)
            {
                await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("NoUpdatesDialogContent"), _resourceLoader.GetString("NoUpdatesDialogTitle"));
                return;
            }

            var dialogResult = await MainWindow.Instance.Content.ShowMessageDialogAsync(string.Format(_resourceLoader.GetString("UpdateAvailableDialogContent"), availableUpdateCount), _resourceLoader.GetString("UpdateAvailableDialogTitle"), showCancel: true);
            if (dialogResult != ContentDialogResult.Primary) return;

            var openedStoreProductPage = await StoreUpdateService.OpenStoreProductPageAsync();
            if (openedStoreProductPage) return;

            await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("OpenStoreFailedDialogContent"), _resourceLoader.GetString("OpenStoreFailedDialogTitle"));
        }
        catch (COMException)
        {
            MainWindow.Instance.HideLoading();
            await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("UpdateCheckFailedDialogContent"), _resourceLoader.GetString("UpdateCheckFailedDialogTitle"));
        }
        catch (InvalidOperationException)
        {
            MainWindow.Instance.HideLoading();
            await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("UpdateCheckFailedDialogContent"), _resourceLoader.GetString("UpdateCheckFailedDialogTitle"));
        }
        catch (UnauthorizedAccessException)
        {
            MainWindow.Instance.HideLoading();
            await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("UpdateCheckFailedDialogContent"), _resourceLoader.GetString("UpdateCheckFailedDialogTitle"));
        }
    }

    [RelayCommand]
    private async Task OpenGitHubRepositoryAsync()
    {
        var openedGitHubRepositoryPage = await Launcher.LaunchUriAsync(s_gitHubRepositoryPageAddress);
        if (openedGitHubRepositoryPage) return;

        await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("OpenGitHubFailedDialogContent"), _resourceLoader.GetString("OpenGitHubFailedDialogTitle"));
    }

    [RelayCommand]
    private void Close() => MainWindow.Instance.Close();

    [RelayCommand]
    private async Task ChangeLanguageAsync(string selectedLanguageTag)
    {
        if (LanguageTagsMatch(GetCurrentLanguageTag(), selectedLanguageTag)) return;

        ApplicationLanguages.PrimaryLanguageOverride = selectedLanguageTag;
        UpdateLanguageMenuFlyoutItems();

        var resourceLoader = new ResourceLoader();
        await MainWindow.Instance.Content.ShowMessageDialogAsync(resourceLoader.GetString("LanguageChangeDialogContent"), resourceLoader.GetString("LanguageChangeDialogTitle"));
    }

    [RelayCommand]
    private void ZoomIn() => WeakReferenceMessenger.Default.Send(new ZoomChangedMessage(true));

    [RelayCommand]
    private void ZoomOut() => WeakReferenceMessenger.Default.Send(new ZoomChangedMessage(false));

    [RelayCommand]
    private void ResetZoom() => WeakReferenceMessenger.Default.Send(ResetImagePreviewZoomMessage.Default);

    private void OnFolderSelected(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return;
        CustomFolderPath = folderPath;
        OutputFolderSetting = OutputFolderSetting.CustomFolder;
    }

    private async Task ConvertImagesAsync()
    {
        var hasPdfFiles = ImageFileViewModels.Any(viewModel => Path.GetExtension(viewModel.FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase));

        if (hasPdfFiles && !IsGhostscriptInstalled())
        {
            await MainWindow.Instance.Content.ShowMessageDialogAsync(_resourceLoader.GetString("GhostscriptRequiredDialogContent"), _resourceLoader.GetString("GhostscriptRequiredDialogTitle"));
            return;
        }

        IsConverting = true;
        ProgressLog.Clear();
        AddProgressLog(_resourceLoader.GetString("ConversionStarted"));

        var formatName = CurrentOutputFormatName;
        var rotationSetting = RotationEnabled ? (RotationSetting)RotationIndex : (RotationSetting)(-1);
        var sizeSetting = SizeSetting;
        var sizeUnit = SizeUnit;
        var resizeInterpolationSetting = (ResizeInterpolationSetting)ResizeInterpolationIndex;
        var width = (uint)Width;
        var height = (uint)Height;
        var prefix = Prefix;
        var format = CurrentOutputFormat;
        var outputFormatSupportsAlpha = DoesOutputFormatSupportAlpha(format);
        var horizontalCropAnchor = (HorizontalCropAnchor)CropHorizontalAnchorIndex;
        var verticalCropAnchor = (VerticalCropAnchor)CropVerticalAnchorIndex;
        var cropBackgroundColor = CreateMagickColor(outputFormatSupportsAlpha ? CropBackgroundColor : CreateOpaqueColor(CropBackgroundColor));
        var transparentColor = CreateMagickColor(outputFormatSupportsAlpha ? TransparentColor : CreateOpaqueColor(TransparentColor));
        var parallelExecution = ParallelExecution;
        var preserveFileDate = PreserveFileDate;
        var preserveExif = PreserveExif;
        var overwriteFile = OverwriteFile;
        var deleteOriginal = DeleteOriginal;
        var preserveAnimation = PreserveAnimation;
        var quality = (uint)Quality;
        var outputFolderSetting = OutputFolderSetting;
        var customFolderPath = CustomFolderPath;
        var subfolderName = SubfolderName;

        string GetOutputDirectory(string sourceFilePath)
        {
            var sourceDirectory = Path.GetDirectoryName(sourceFilePath);
            if (outputFolderSetting == OutputFolderSetting.SameFolder) return sourceDirectory;
            if (outputFolderSetting == OutputFolderSetting.PhotoFolder) return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (outputFolderSetting == OutputFolderSetting.CustomFolder && !string.IsNullOrEmpty(customFolderPath)) return customFolderPath;
            if (outputFolderSetting == OutputFolderSetting.Subfolder && !string.IsNullOrEmpty(subfolderName)) return Path.Combine(sourceDirectory, subfolderName);
            return sourceDirectory;
        }

        async Task ConvertSingleImageAsync(ImageFileViewModel viewModel)
        {
            var directoryPath = GetOutputDirectory(viewModel.FilePath);
            Directory.CreateDirectory(directoryPath);
            AddProgressLog(string.Format(_resourceLoader.GetString("ConvertingFile"), viewModel.FileName));

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

            var originalCreationTime = preserveFileDate ? File.GetCreationTime(viewModel.FilePath) : default;
            var originalLastWriteTime = preserveFileDate ? File.GetLastWriteTime(viewModel.FilePath) : default;

            if (preserveAnimation && Constants.AnimatedOutputFormats.Contains(formatName) && Constants.AnimatedInputExtensions.Contains(Path.GetExtension(viewModel.FilePath)))
            {
                var isAnimated = await Task.Run(() =>
                {
                    using var collection = new MagickImageCollection(viewModel.FilePath);
                    if (collection.Count <= 1) return false;

                    foreach (var frame in collection)
                    {
                        var magickFrame = (MagickImage)frame;
                        if (rotationSetting >= 0) RotateImage(magickFrame, rotationSetting);
                        CleanTransparentPixels(magickFrame, transparentColor, outputFormatSupportsAlpha);
                        ResizeImage(magickFrame, sizeSetting, sizeUnit, resizeInterpolationSetting, width, height, horizontalCropAnchor, verticalCropAnchor, cropBackgroundColor);
                        if (!preserveExif) magickFrame.Strip();
                        else if (rotationSetting > 0) magickFrame.SetAttribute("exif:Orientation", "1");
                        magickFrame.Quality = quality;
                    }

                    collection.Write(filePath);
                    return true;
                });

                if (isAnimated)
                {
                    if (preserveFileDate)
                    {
                        File.SetCreationTime(filePath, originalCreationTime);
                        File.SetLastWriteTime(filePath, originalLastWriteTime);
                    }

                    AddProgressLog(string.Format(_resourceLoader.GetString("ConversionFileComplete"), viewModel.FileName, fileName));

                    if (deleteOriginal && filePath != viewModel.FilePath)
                    {
                        ImageFileViewModels.Remove(viewModel);
                        UpdateImageListDependentProperties();
                        File.Delete(viewModel.FilePath);
                    }
                    return;
                }
            }

            using var image = CreateConversionMagickImage(viewModel, sizeSetting, sizeUnit, width, height);

            if (formatName != "ICO") await Task.Run(() =>
            {
                if (rotationSetting >= 0) RotateImage(image, rotationSetting);
                CleanTransparentPixels(image, transparentColor, outputFormatSupportsAlpha);
                ResizeImage(image, sizeSetting, sizeUnit, resizeInterpolationSetting, width, height, horizontalCropAnchor, verticalCropAnchor, cropBackgroundColor);
            });
            else
            {
                using var collection = new MagickImageCollection();

                uint[] sizes = [16, 32, 48, 64, 128, 256];

                foreach (var size in sizes)
                {
                    var iconImage = viewModel.CreateMagickImage(size, size);
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

                AddProgressLog(string.Format(_resourceLoader.GetString("ConversionFileComplete"), viewModel.FileName, fileName));

                if (deleteOriginal && filePath != viewModel.FilePath)
                {
                    ImageFileViewModels.Remove(viewModel);
                    UpdateImageListDependentProperties();
                    File.Delete(viewModel.FilePath);
                }
                return;
            }

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

            AddProgressLog(string.Format(_resourceLoader.GetString("ConversionFileComplete"), viewModel.FileName, fileName));

            if (deleteOriginal && filePath != viewModel.FilePath)
            {
                ImageFileViewModels.Remove(viewModel);
                UpdateImageListDependentProperties();
                File.Delete(viewModel.FilePath);
            }
        }

        if (parallelExecution)
        {
            await Parallel.ForEachAsync(ImageFileViewModels.ToList(), async (viewModel, _) => await ConvertSingleImageAsync(viewModel));
        }
        else
        {
            foreach (var viewModel in ImageFileViewModels.ToList())
            {
                await ConvertSingleImageAsync(viewModel);
            }
        }

        AddProgressLog(_resourceLoader.GetString("ConversionComplete"));

        await MainWindow.Instance.Content.ShowMessageDialogAsync(string.Format(_resourceLoader.GetString("ConversionCompleteDialogContent"), ImageFileViewModels.Count), _resourceLoader.GetString("ConversionCompleteDialogTitle"));

        IsConverting = false;
        ProgressLog.Clear();
    }

    public void AddProgressLog(string message)
    {
        var dispatcherQueue = MainWindow.Instance.DispatcherQueue;
        if (dispatcherQueue.HasThreadAccess) ProgressLog.Add(message);
        else dispatcherQueue.TryEnqueue(() => ProgressLog.Add(message));
    }

    private void SaveSettings()
    {
        _localSettings.Values["FormatIndex"] = FormatIndex;
        _localSettings.Values["Quality"] = Quality;
        _localSettings.Values["RotationEnabled"] = RotationEnabled;
        _localSettings.Values["RotationIndex"] = RotationIndex;
        _localSettings.Values["SizeSettingIndex"] = SizeSettingIndex;
        _localSettings.Values["SizeUnitIndex"] = SizeUnitIndex;
        _localSettings.Values["ResizeInterpolationSettingIndex"] = ResizeInterpolationIndex;
        _localSettings.Values["Width"] = Width;
        _localSettings.Values["Height"] = Height;
        _localSettings.Values["CropHorizontalAnchorIndex"] = CropHorizontalAnchorIndex;
        _localSettings.Values["CropVerticalAnchorIndex"] = CropVerticalAnchorIndex;
        _localSettings.Values["CropBackgroundColor"] = ConvertColorToSettingValue(CropBackgroundColor);
        _localSettings.Values["TransparentColor"] = ConvertColorToSettingValue(TransparentColor);
        _localSettings.Values["Prefix"] = Prefix;
        _localSettings.Values["OverwriteFile"] = OverwriteFile;
        _localSettings.Values["OutputFolderSetting"] = (int)OutputFolderSetting;
        _localSettings.Values["CustomFolderPath"] = CustomFolderPath;
        _localSettings.Values["SubfolderName"] = SubfolderName;
        _localSettings.Values["ParallelExecution"] = ParallelExecution;
        _localSettings.Values["PreserveFileDate"] = PreserveFileDate;
        _localSettings.Values["PreserveExif"] = PreserveExif;
        _localSettings.Values["PreserveAnimation"] = PreserveAnimation;
        _localSettings.Values["DeleteOriginal"] = DeleteOriginal;
    }

    private void LoadSettings()
    {
        var values = _localSettings.Values;
        if (!values.TryGetValue("FormatIndex", out object value)) return;

        FormatIndex = (int)value;
        Quality = (double)values["Quality"];
        RotationEnabled = (bool)values["RotationEnabled"];
        RotationIndex = (int)values["RotationIndex"];
        SizeSettingIndex = (int)values["SizeSettingIndex"];
        SizeUnitIndex = (int)values["SizeUnitIndex"];
        if (values.TryGetValue("ResizeInterpolationSettingIndex", out var resizeInterpolationSettingIndex)) ResizeInterpolationIndex = (int)resizeInterpolationSettingIndex;
        Width = (double)values["Width"];
        Height = (double)values["Height"];
        if (values.TryGetValue("CropHorizontalAnchorIndex", out var cropHorizontalAnchorIndex)) CropHorizontalAnchorIndex = (int)cropHorizontalAnchorIndex;
        if (values.TryGetValue("CropVerticalAnchorIndex", out var cropVerticalAnchorIndex)) CropVerticalAnchorIndex = (int)cropVerticalAnchorIndex;
        if (values.TryGetValue("CropBackgroundColor", out var cropBackgroundColorSettingValue) && cropBackgroundColorSettingValue is string cropBackgroundColorText) CropBackgroundColor = ConvertSettingValueToColor(cropBackgroundColorText);
        if (values.TryGetValue("TransparentColor", out var transparentColorSettingValue) && transparentColorSettingValue is string transparentColorText) TransparentColor = ConvertSettingValueToColor(transparentColorText);
        Prefix = (string)values["Prefix"];
        OverwriteFile = (bool)values["OverwriteFile"];

        OutputFolderSetting = (OutputFolderSetting)(int)values["OutputFolderSetting"];

        CustomFolderPath = (string)values["CustomFolderPath"];
        SubfolderName = (string)values["SubfolderName"];
        ParallelExecution = (bool)values["ParallelExecution"];
        PreserveFileDate = (bool)values["PreserveFileDate"];
        PreserveExif = (bool)values["PreserveExif"];
        if (values.TryGetValue("PreserveAnimation", out var preserveAnimationValue)) PreserveAnimation = (bool)preserveAnimationValue;
        DeleteOriginal = (bool)values["DeleteOriginal"];
    }

    private void ResetSettings()
    {
        _localSettings.Values.Clear();

        FormatIndex = 0;
        Quality = 80;
        RotationEnabled = true;
        RotationIndex = 0;
        SizeSettingIndex = 0;
        SizeUnitIndex = 0;
        ResizeInterpolationIndex = 0;
        Width = 0;
        Height = 0;
        CropHorizontalAnchorIndex = 1;
        CropVerticalAnchorIndex = 1;
        CropBackgroundColor = CreateDefaultCropBackgroundColor();
        TransparentColor = CreateDefaultTransparentColor();
        Prefix = "ATIC_";
        OverwriteFile = true;
        OutputFolderSetting = OutputFolderSetting.SameFolder;
        CustomFolderPath = "";
        SubfolderName = "output";
        ParallelExecution = true;
        PreserveFileDate = true;
        PreserveExif = true;
        PreserveAnimation = true;
        DeleteOriginal = false;
    }
}
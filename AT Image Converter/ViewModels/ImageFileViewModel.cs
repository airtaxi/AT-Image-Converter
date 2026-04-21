using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using System;
using System.IO;

namespace ImageConverterAT.ViewModels;

public class ImageFileViewModel(string filePath) : ObservableObject
{
    public string FilePath { get; init; } = filePath;
    public string FileName { get; init; } = Path.GetFileName(filePath);
    public bool IsSvgFile => Path.GetExtension(FilePath).Equals(".svg", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new MagickImage from the file path. Caller is responsible for disposing the returned image.
    /// </summary>
    public MagickImage CreateMagickImage(uint? rasterizedWidth = null, uint? rasterizedHeight = null)
    {
        var image = new MagickImage();

        if (IsSvgFile)
        {
            image.BackgroundColor = MagickColors.Transparent;
            var readSettings = new MagickReadSettings
            {
                BackgroundColor = MagickColors.Transparent
            };
            if (rasterizedWidth.HasValue) readSettings.Width = rasterizedWidth.Value;
            if (rasterizedHeight.HasValue) readSettings.Height = rasterizedHeight.Value;
            image.Read(FilePath, readSettings);
            image.Alpha(AlphaOption.On);
            image.BackgroundColor = MagickColors.Transparent;
            return image;
        }

        image.Read(FilePath);
        return image;
    }
}

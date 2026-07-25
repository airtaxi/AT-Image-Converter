using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using System;
using System.IO;
using System.Linq;

namespace ImageConverterAT.ViewModels;

public class ImageFileViewModel(string filePath) : ObservableObject
{
    public string FilePath { get; init; } = filePath;
    public string FileName { get; init; } = Path.GetFileName(filePath);
    public bool IsSvgFile => Path.GetExtension(FilePath).Equals(".svg", StringComparison.OrdinalIgnoreCase);
    public bool IsIcoFile => Path.GetExtension(FilePath).Equals(".ico", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new MagickImage from the file path. Caller is responsible for disposing the returned image.
    /// </summary>
    public MagickImage CreateMagickImage(uint? rasterizedWidth = null, uint? rasterizedHeight = null)
    {
        if (IsSvgFile)
        {
            var image = new MagickImage();
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

        if (IsIcoFile)
        {
            using var collection = new MagickImageCollection(FilePath);
            if (collection.Count > 0)
            {
                var largestFrame = collection.OrderByDescending(frame => frame.Width).ThenByDescending(frame => frame.Height).First();
                return (MagickImage)largestFrame.Clone();
            }
        }

        var image = new MagickImage();
        image.Read(FilePath);
        return image;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using System;
using System.IO;

namespace ImageConverterAT.ViewModels;

public class ImageFileViewModel(string filePath) : ObservableObject
{
    public string FilePath { get; init; } = filePath;
    public string FileName { get; init; } = Path.GetFileName(filePath);

    /// <summary>
    /// Creates a new MagickImage from the file path. Caller is responsible for disposing the returned image.
    /// </summary>
    public MagickImage CreateMagickImage()
    {
        var image = new MagickImage();

        if (Path.GetExtension(FilePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            image.BackgroundColor = MagickColors.Transparent;

        image.Read(FilePath);
        return image;
    }
}

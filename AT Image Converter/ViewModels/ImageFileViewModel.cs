using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using System;
using System.IO;

namespace ImageConverterAT.ViewModels;

public class ImageFileViewModel : ObservableObject
{
    public string FilePath { get; init; }
    public string FileName { get; init; }

    public ImageFileViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

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

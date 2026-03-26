using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagick;
using System;
using System.IO;

namespace ImageConverterAT.ViewModels;

public class ImageFileViewModel : ObservableObject, IDisposable
{
    public string FilePath { get; init; }
    public string FileName { get; init; }
    public MagickImage MagickImage { get; init; }

    public ImageFileViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        MagickImage = CreateMagickImage(filePath);
    }

    private static MagickImage CreateMagickImage(string filePath)
    {
        var image = new MagickImage();

        if (Path.GetExtension(filePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            image.BackgroundColor = MagickColors.Transparent;

        image.Read(filePath);
        return image;
    }

    public void Dispose()
    {
        MagickImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}

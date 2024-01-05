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
        MagickImage = new MagickImage(filePath);
    }

    public void Dispose()
    {
        MagickImage?.Dispose();
        GC.SuppressFinalize(this);
    }
}

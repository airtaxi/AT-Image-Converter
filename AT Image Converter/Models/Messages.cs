using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;

namespace ImageConverterAT.Models;

public sealed class AddImageFilesMessage(IEnumerable<string> filePaths) : ValueChangedMessage<IEnumerable<string>>(filePaths)
{
    public IEnumerable<string> FilePaths { get; } = filePaths;
}

public sealed class ShowFileOpenPickerMessage : ValueChangedMessage<bool>
{
    public static ShowFileOpenPickerMessage Instance { get; } = new();
    private ShowFileOpenPickerMessage() : base(true) { }
}

public sealed class ShowFolderPickerMessage : ValueChangedMessage<bool>
{
    public static ShowFolderPickerMessage Instance { get; } = new();
    private ShowFolderPickerMessage() : base(true) { }
}

public sealed class FolderSelectedMessage(string folderPath) : ValueChangedMessage<string>(folderPath)
{
    public string FolderPath { get; } = folderPath;
}

public sealed class ResetImagePreviewZoomMessage(ImageDimensions dimensions) : ValueChangedMessage<ImageDimensions>(dimensions)
{
    public ImageDimensions Dimensions { get; } = dimensions;
    public static ResetImagePreviewZoomMessage Default { get; } = new(ImageDimensions.Default);
}

public sealed class ZoomChangedMessage(bool isZoomIn) : ValueChangedMessage<bool>(isZoomIn)
{
    public bool IsZoomIn { get; } = isZoomIn;
}
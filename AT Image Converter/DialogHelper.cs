using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace ImageConverterAT;

public static class DialogHelper
{
    private static readonly ResourceLoader _resourceLoader = new();

    public static async Task<ContentDialogResult> ShowMessageDialogAsync(this UIElement element, string description, string title, bool showCancel = false)
    {
        var contentDialogPopup = VisualTreeHelper.GetOpenPopupsForXamlRoot(element.XamlRoot).FirstOrDefault(x => x.Child is ContentDialog);
        if (contentDialogPopup != null)
        {
            var contentDialog = contentDialogPopup.Child as ContentDialog;
            contentDialog.Hide();
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = description,
            PrimaryButtonText = _resourceLoader.GetString("DialogOkButton"),
            XamlRoot = element.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        if (showCancel) dialog.SecondaryButtonText = _resourceLoader.GetString("DialogCancelButton");

        return await dialog.ShowAsync();
    }
}
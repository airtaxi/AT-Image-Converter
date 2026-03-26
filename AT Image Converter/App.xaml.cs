using ImageConverterAT.Helpers;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources;

namespace ImageConverterAT;

public partial class App : Application
{
    static App()
    {
    }

    private Window _window;

	public App()
    {
        InitializeComponent();
		try { FileExplorerContextMenuHelper.TryAddProgramToSendToContextMenu(); } catch { }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = new MainWindow();
		_window.Activate();
    }
}

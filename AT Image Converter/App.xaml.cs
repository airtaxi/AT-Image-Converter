using ImageConverterAT.Helpers;
using Microsoft.UI.Xaml;

namespace ImageConverterAT;

public partial class App : Application
{
	private Window _window;

	public App()
	{
		InitializeComponent();
		FileExplorerContextMenuHelper.TryAddProgramToSendToContextMenu();
	}

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = new MainWindow();
		_window.Activate();
	}
}

using ImageConverterAT.Helpers;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Resources;

namespace ImageConverterAT;

public partial class App : Application
{
    private Window _window;

	public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = new MainWindow();
		_window.Activate();
    }
}

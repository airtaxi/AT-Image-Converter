using ImageConverterAT.Helpers;
using Microsoft.UI.Xaml;
using System.Reflection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;

namespace ImageConverterAT;

public partial class App : Application
{
    private const int UpdateCheckIntervalInMinutes = 10;
    private static readonly Timer UpdateCheckTimer;

    static App()
    {
        UpdateCheckTimer = new(UpdateCheckTimerCallback, null, (int)TimeSpan.FromMinutes(UpdateCheckIntervalInMinutes).TotalMilliseconds, Timeout.Infinite);
    }

    private const string UpdateAvailableTitle = "Update Available";
    private static async void UpdateCheckTimerCallback(object state) => await CheckForUpdateAsync();

    private static async Task CheckForUpdateAsync()
    {
        try
        {
            var url = "https://raw.githubusercontent.com/airtaxi/AT-Image-Converter/master/latest";
            var remoteVersionString = await HttpHelper.GetContentFromUrlAsync(url);
            if (string.IsNullOrEmpty(remoteVersionString)) return;

            var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var remoteVersion = new Version(remoteVersionString);
            if (localVersion >= remoteVersion) return;

            var configurationKey = "versionChecked" + remoteVersionString;
            var hasNotificationShownForRemoteVersion = Configuration.GetValue<bool?>(configurationKey) ?? false;
            if (hasNotificationShownForRemoteVersion) return;
            Configuration.SetValue(configurationKey, true);

            var builder = new ToastContentBuilder()
            .AddText(UpdateAvailableTitle)
            .AddText($"A new version ({remoteVersion}) is available.\nDo you want to download it?")
            .AddArgument("versionString", remoteVersionString);
            builder.Show();
        }
        finally { UpdateCheckTimer.Change((int)TimeSpan.FromMinutes(UpdateCheckIntervalInMinutes).TotalMilliseconds, Timeout.Infinite); }
    }

    private Window _window;

	public App()
    {
        ToastNotificationManagerCompat.OnActivated += OnToastNotificationActivated;

        InitializeComponent();
		FileExplorerContextMenuHelper.TryAddProgramToSendToContextMenu();
    }

    private static void OnToastNotificationActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        ToastArguments args = ToastArguments.Parse(toastArgs.Argument);
        var versionString = args["versionString"];
        if (versionString != null)
        {
            var url = "https://github.com/airtaxi/AT-Image-Converter/releases/tag/" + versionString;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = new MainWindow();
		_window.Activate();
        await CheckForUpdateAsync();
    }
}

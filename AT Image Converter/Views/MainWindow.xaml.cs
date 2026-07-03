using ImageConverterAT.Pages;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;

namespace ImageConverterAT;

public sealed partial class MainWindow : Window
{
    public static MainWindow Instance { get; private set; }

    public MainWindow()
    {
        Instance = this;

        InitializeComponent();

        // Setup window
        AppWindow.SetIcon("Assets/Icon.ico");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Add files from command line arguments if any
        var launchFilePaths = GetLaunchFilePaths();
        AppFrame.Navigate(typeof(MainPage), launchFilePaths);
    }

    private static string[] GetLaunchFilePaths()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 2 && args[1] == "--file-list")
        {
            var fileListPath = args[2];
            if (File.Exists(fileListPath))
            {
                var filePaths = File.ReadAllLines(fileListPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                try { File.Delete(fileListPath); } catch { }
                return filePaths;
            }
        }
        else if (args.Length > 1)
        {
            return args[1..];
        }
        return [];
    }

    public void ShowLoading(string message)
    {
        if (DispatcherQueue.HasThreadAccess) SetLoadingState(Visibility.Visible, message);
        else DispatcherQueue.TryEnqueue(() => SetLoadingState(Visibility.Visible, message));
    }

    public void HideLoading()
    {
        if (DispatcherQueue.HasThreadAccess) SetLoadingState(Visibility.Collapsed, null);
        else DispatcherQueue.TryEnqueue(() => SetLoadingState(Visibility.Collapsed, null));
    }

    private void SetLoadingState(Visibility visibility, string message)
    {
        LoadingGrid.Visibility = visibility;
        if (!string.IsNullOrEmpty(message) && visibility == Visibility.Visible)
        {
            LoadingTextBlock.Text = message;
            LoadingTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            LoadingTextBlock.Visibility = Visibility.Collapsed;
            LoadingTextBlock.Text = "";
        }
    }
}
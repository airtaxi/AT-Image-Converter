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
        FrMain.Navigate(typeof(MainPage), launchFilePaths);
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
        DispatcherQueue.TryEnqueue(() =>
        {
            GdLoading.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(message))
            {
                TbLoading.Text = message;
                TbLoading.Visibility = Visibility.Visible;
            }
            else TbLoading.Visibility = Visibility.Collapsed;
        });
    }

    public void HideLoading()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            GdLoading.Visibility = Visibility.Collapsed;
            TbLoading.Visibility = Visibility.Collapsed;
            TbLoading.Text = "";
        });
    }
}
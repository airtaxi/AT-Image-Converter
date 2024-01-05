using Microsoft.Win32;
using ShellLink;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ImageConverterAT.Helpers;

public static class FileExplorerContextMenuHelper
{
	private const string ApplicationName= "AT Image Converter";
	private readonly static string ExecutableBinaryPath = Environment.ProcessPath;

	public static void TryAddProgramToSendToContextMenu()
	{
		// Compose shortcut path for SendTo folder
		string sendToPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft\\Windows\\SendTo");
		string shortcutPath = Path.Combine(sendToPath, ApplicationName + ".lnk");

		// Check if shortcut already exists
		if (File.Exists(shortcutPath)) return; 

		// Create shortcut
		Shortcut.CreateShortcut(ExecutableBinaryPath).WriteToFile(shortcutPath);
	}
}

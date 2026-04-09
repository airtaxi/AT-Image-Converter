using System;
using System.Collections.Generic;

namespace ImageConverterAT;

public static class Constants
{
    public readonly static string[] ImageFileFormats = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".ico", ".svg", ".webp", ".heic", ".heif", ".heix", ".pdf", ".psd", ".xcf", ".raw"];

    public readonly static HashSet<string> AnimatedInputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gif", ".webp"
    };

    public readonly static HashSet<string> AnimatedOutputFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "WEBP"
    };
}

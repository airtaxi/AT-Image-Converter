using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading;

namespace ImageConverterAT;

public class Configuration
{
	private readonly static object LockObject = new();
	private readonly static string BasePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private const string ConfigurationDirectoryName = "ImageConverterAT";

    private const string ConfigurationFileName = "settings.json";
    private const string ConfigurationBackupFileName = "settings.json.bak";

    private readonly static string ConfigurationDirectoryPath = Path.Combine(BasePath, ConfigurationDirectoryName);
    private readonly static string ConfigurationFilePath = Path.Combine(ConfigurationDirectoryPath, ConfigurationFileName);
    private readonly static string ConfigurationBackupFilePath = Path.Combine(ConfigurationDirectoryPath, ConfigurationBackupFileName);

    private static Dictionary<string, object> s_cache;

    private static void ValidateConfigurationFile()
    {
        if (!Directory.Exists(ConfigurationDirectoryPath))
            Directory.CreateDirectory(ConfigurationDirectoryPath);
        if (!File.Exists(ConfigurationFilePath))
            File.Create(ConfigurationFilePath).Close();
        if (!File.Exists(ConfigurationBackupFilePath))
            File.Create(ConfigurationBackupFilePath).Close();
    }

    private static string GetConfigurationFileContentString()
    {
        Monitor.Enter(LockObject);
        try
        {
            ValidateConfigurationFile();
            var content = File.ReadAllText(ConfigurationFilePath).Trim();
            try { JsonNode.Parse(content); }
            catch (Exception) { content = File.ReadAllText(ConfigurationBackupFilePath).Trim(); }
            if (string.IsNullOrEmpty(content)) content = "{}";
            return content;
        }
        finally { Monitor.Exit(LockObject); }
    }

    public static Dictionary<string, object> GetConfigurationFileContent()
    {
        if (s_cache == null)
        {
            var configurationFileContentString = GetConfigurationFileContentString();
            var convertedFileContent = JsonSerializer.Deserialize<Dictionary<string, object>>(configurationFileContentString);
            s_cache = new Dictionary<string, object>(convertedFileContent);
        }
        return s_cache;
    }

    public static T GetValue<T>(string key)
    {
        Monitor.Enter(LockObject);
        try
        {
            var convertedFileContent = GetConfigurationFileContent();
            if (!convertedFileContent.TryGetValue(key, out object rawValue)) return default;
            if (rawValue is JsonElement element)
            {
                var value = element.Deserialize<T>();
                convertedFileContent[key] = value;
                return value;
            }
            else if (rawValue is JsonArray array)
            {
                var value = array.Deserialize<T>();
                convertedFileContent[key] = value;
                return value;
            }
            else if (rawValue is T value) return value;
            else return default;
        }
        finally { Monitor.Exit(LockObject); }
    }

    private static string s_buffer;
    private static System.Timers.Timer timer;

    public static void SetValue(string key, object value)
    {
        Monitor.Enter(LockObject);
        try
        {
            var convertedFileContent = GetConfigurationFileContent();
            if (convertedFileContent.ContainsKey(key)) convertedFileContent[key] = value;
            else convertedFileContent.TryAdd(key, value);
            s_buffer = JsonSerializer.Serialize(convertedFileContent);

            if (timer == null)
            {
                timer = new() { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    WriteBuffer();
                };
                timer.Interval = 50;
            }
            timer.Stop();
            timer.Start();
        }
        finally { Monitor.Exit(LockObject); }
    }

    public static void Import(string json)
    {
        Monitor.Enter(LockObject);
        try
        {
            var content = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            var convertedFileContent = GetConfigurationFileContent();
            foreach (var (key, value) in content)
            {
                if (convertedFileContent.ContainsKey(key)) convertedFileContent[key] = value;
                else convertedFileContent.TryAdd(key, value);
            }
            s_cache = new(convertedFileContent);
            s_buffer = JsonSerializer.Serialize(convertedFileContent);
            WriteBuffer();
        }
        finally { Monitor.Exit(LockObject); }
    }

    public static string Export()
    {
        Monitor.Enter(LockObject);
        try
        {
            var convertedFileContent = GetConfigurationFileContent();
            return JsonSerializer.Serialize(convertedFileContent);
        }
        finally { Monitor.Exit(LockObject); }
    }

    public static bool IsExiting { get; set; }
    private static bool s_exited = false;
    public static void WriteBuffer()
    {
        if (IsExiting && !s_exited)
        {
            File.WriteAllText(ConfigurationFilePath, s_buffer);
            s_exited = true;
        }
        else if (!IsExiting)
        {
            Monitor.Enter(LockObject);
            try
            {
                File.WriteAllText(ConfigurationFilePath, s_buffer);
                File.WriteAllText(ConfigurationBackupFilePath, s_buffer);
            }
            finally { Monitor.Exit(LockObject); }
        }
    }
}
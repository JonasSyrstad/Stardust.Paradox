using System;
using System.IO;

namespace Stardust.Paradox.GremlinStudio.Core.Storage;

public static class AppDataPaths
{
    internal const string AppFolderName = "GremlinStudio";

    internal static string GetAppDataDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, AppFolderName);
    }

    internal static string EnsureAppDataDirectoryExists()
    {
        var dir = GetAppDataDirectory();
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ConnectionsFilePath => Path.Combine(EnsureAppDataDirectoryExists(), "connections.json");

    public static string ThemePreferenceFilePath => Path.Combine(EnsureAppDataDirectoryExists(), "theme.txt");

    public static string LastConnectionPreferenceFilePath => Path.Combine(EnsureAppDataDirectoryExists(), "last-connection.txt");

    public static string SnippetsFilePath => Path.Combine(EnsureAppDataDirectoryExists(), "snippets.json");

    public static string VariablesFilePath => Path.Combine(EnsureAppDataDirectoryExists(), "variables.json");
}

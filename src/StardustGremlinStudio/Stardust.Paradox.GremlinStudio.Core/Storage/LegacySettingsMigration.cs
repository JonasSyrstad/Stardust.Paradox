using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Stardust.Paradox.GremlinStudio.Core.Storage;

public static class LegacySettingsMigration
{
    public static void MigrateIfNeeded(ILogger logger)
    {
        try
        {
            var targetDir = AppDataPaths.EnsureAppDataDirectoryExists();

            var legacyRoots = new[]
            {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (var root in legacyRoots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;

                MigrateFileIfMissing(logger, root, targetDir, "connections.json");
                MigrateFileIfMissing(logger, root, targetDir, "theme.txt");
                MigrateFileIfMissing(logger, root, targetDir, "query-history.json");
                MigrateFileIfMissing(logger, root, targetDir, "last-connection.txt");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed while migrating legacy settings");
        }
    }

    private static void MigrateFileIfMissing(ILogger logger, string legacyRoot, string targetDir, string fileName)
    {
        var sourcePath = Path.Combine(legacyRoot, fileName);
        if (!File.Exists(sourcePath))
            return;

        var targetPath = Path.Combine(targetDir, fileName);
        if (File.Exists(targetPath))
            return;

        try
        {
            File.Copy(sourcePath, targetPath, overwrite: false);
            logger.LogInformation("Migrated legacy settings file '{FileName}' from '{Source}' to '{Target}'", fileName, sourcePath, targetPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to migrate legacy file '{FileName}' from '{Source}'", fileName, sourcePath);
        }
    }
}

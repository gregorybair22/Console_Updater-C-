using System.IO;

namespace Updater.Core.Services;

public static class DirectoryHelper
{
    /// <summary>
    /// Moves a directory from source to destination, handling cross-volume moves by copying and deleting.
    /// </summary>
    public static void MoveDirectory(string sourcePath, string destinationPath, ILogger? logger = null)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourcePath}");
        }

        if (Directory.Exists(destinationPath))
        {
            throw new InvalidOperationException($"Destination directory already exists: {destinationPath}");
        }

        // Check if source and destination are on the same volume
        var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
        var destRoot = Path.GetPathRoot(Path.GetFullPath(destinationPath));

        if (string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase))
        {
            // Same volume - use fast move operation
            Directory.Move(sourcePath, destinationPath);
        }
        else
        {
            // Different volumes - copy then delete
            logger?.LogInfo($"Moving across volumes: {sourceRoot} -> {destRoot} (using copy + delete)");
            CopyDirectory(sourcePath, destinationPath, logger);
            Directory.Delete(sourcePath, true);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, ILogger? logger = null)
    {
        Directory.CreateDirectory(destDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        // Recursively copy subdirectories
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir, logger);
        }
    }
}


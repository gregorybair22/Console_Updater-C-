using System;
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

        // Normalize paths - remove trailing separators and get full paths
        var normalizedSource = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDest = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Check if destination is a subdirectory of source (would cause infinite loop)
        if (normalizedDest.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedDest.StartsWith(normalizedSource + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Destination cannot be a subdirectory of source: {normalizedDest}");
        }

        if (Directory.Exists(normalizedDest))
        {
            throw new InvalidOperationException($"Destination directory already exists: {normalizedDest}");
        }

        // Ensure destination parent directory exists
        var destParent = Path.GetDirectoryName(normalizedDest);
        if (!string.IsNullOrEmpty(destParent) && !Directory.Exists(destParent))
        {
            Directory.CreateDirectory(destParent);
        }

        // Check if source and destination are on the same volume
        var sourceRoot = Path.GetPathRoot(normalizedSource);
        var destRoot = Path.GetPathRoot(normalizedDest);

        if (string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase))
        {
            // Same volume - use fast move operation
            try
            {
                Directory.Move(normalizedSource, normalizedDest);
            }
            catch (IOException ex) when (ex.Message.Contains("parameter", StringComparison.OrdinalIgnoreCase) || 
                                         ex.Message.Contains("incorrect", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to copy+delete if move fails with parameter error
                logger?.LogWarn($"Directory.Move failed, falling back to copy+delete: {ex.Message}");
                CopyDirectory(normalizedSource, normalizedDest, logger);
                Directory.Delete(normalizedSource, true);
            }
        }
        else
        {
            // Different volumes - copy then delete
            logger?.LogInfo($"Moving across volumes: {sourceRoot} -> {destRoot} (using copy + delete)");
            CopyDirectory(normalizedSource, normalizedDest, logger);
            Directory.Delete(normalizedSource, true);
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


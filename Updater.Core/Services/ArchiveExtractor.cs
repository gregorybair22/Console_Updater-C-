using System.Diagnostics;
using System.IO.Compression;

namespace Updater.Core.Services;

public class ArchiveExtractor
{
    private readonly ILogger _logger;

    public ArchiveExtractor(ILogger logger)
    {
        _logger = logger;
    }

    public string Extract(string sourcePath, string destinationPath, string? sevenZipPath = null)
    {
        if (Directory.Exists(sourcePath))
        {
            // Source is already a folder - copy it to destination
            _logger.LogInfo($"Copying folder: {sourcePath}");
            
            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, true);
            }
            Directory.CreateDirectory(destinationPath);

            // Copy all files and subdirectories
            CopyDirectory(sourcePath, destinationPath);
            
            _logger.LogInfo($"Folder copy completed to: {destinationPath}");
            return destinationPath;
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source not found: {sourcePath}");
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        return extension switch
        {
            ".zip" => ExtractZip(sourcePath, destinationPath),
            ".rar" => ExtractRar(sourcePath, destinationPath, sevenZipPath),
            _ => throw new NotSupportedException($"Unsupported archive format: {extension}")
        };
    }

    private string ExtractZip(string zipPath, string destinationPath)
    {
        _logger.LogInfo($"Extracting ZIP archive: {zipPath}");
        
        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }
        Directory.CreateDirectory(destinationPath);

        ZipFile.ExtractToDirectory(zipPath, destinationPath);
        
        _logger.LogInfo($"ZIP extraction completed to: {destinationPath}");
        return destinationPath;
    }

    private string ExtractRar(string rarPath, string destinationPath, string? sevenZipPath)
    {
        _logger.LogInfo($"Extracting RAR archive: {rarPath}");

        // Try to find 7z.exe
        var sevenZipExe = FindSevenZip(sevenZipPath);
        if (string.IsNullOrEmpty(sevenZipExe))
        {
            throw new FileNotFoundException(
                "7z.exe not found. Please install 7-Zip and ensure 7z.exe is in PATH, " +
                "or specify --sevenzip-path option.");
        }

        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }
        Directory.CreateDirectory(destinationPath);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = sevenZipExe,
            Arguments = $"x \"{rarPath}\" -o\"{destinationPath}\" -y",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start 7z.exe process");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"7z.exe extraction failed with exit code {process.ExitCode}: {error}");
        }

        _logger.LogInfo($"RAR extraction completed to: {destinationPath}");
        return destinationPath;
    }

    private string? FindSevenZip(string? customPath)
    {
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        // Check PATH
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            var sevenZipPath = Path.Combine(dir, "7z.exe");
            if (File.Exists(sevenZipPath))
            {
                return sevenZipPath;
            }
        }

        // Check common installation locations
        var commonPaths = new[]
        {
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}


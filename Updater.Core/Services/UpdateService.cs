using System.IO;
using Updater.Core.Models;

namespace Updater.Core.Services;

public class UpdateService
{
    private readonly ILogger _logger;
    private readonly ArchiveExtractor _extractor;
    private readonly FileLockDetector _lockDetector;

    public UpdateService(ILogger logger, ArchiveExtractor extractor, FileLockDetector lockDetector)
    {
        _logger = logger;
        _extractor = extractor;
        _lockDetector = lockDetector;
    }

    public void Update(UpdateOptions options)
    {
        _logger.LogInfo("Starting update process...");

        // Validate inputs
        ValidateOptions(options);

        // Create unique temp folder
        var tempFolder = Path.Combine(Path.GetTempPath(), $"updater_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);

        try
        {
            // Expand source
            _logger.LogInfo($"Expanding source: {options.Source}");
            var expandedRoot = _extractor.Extract(options.Source, tempFolder, options.WinRarPath);

            // Resolve inner folder
            var innerFolder = ResolveInnerFolder(expandedRoot, options.InnerFolder);
            var newBuildPath = string.IsNullOrEmpty(innerFolder) 
                ? expandedRoot 
                : Path.Combine(expandedRoot, innerFolder);

            if (!Directory.Exists(newBuildPath))
            {
                throw new DirectoryNotFoundException($"Inner folder not found: {innerFolder}");
            }

            // Preserve config
            if (options.PreserveConfig && Directory.Exists(options.Destination))
            {
                PreserveConfigFile(options, newBuildPath);
            }

            // Detect locked files
            if (Directory.Exists(options.Destination))
            {
                _logger.LogInfo("Checking for locked files in destination...");
                var lockedFiles = _lockDetector.DetectLockedFiles(options.Destination);
                
                if (lockedFiles.Count > 0)
                {
                    _logger.LogError("Locked files detected:");
                    foreach (var file in lockedFiles)
                    {
                        _logger.LogError($"  - {file}");
                    }
                    throw new InvalidOperationException($"Cannot proceed: {lockedFiles.Count} file(s) are locked or in use.");
                }
            }

            if (options.DryRun)
            {
                _logger.LogInfo("[DRY RUN] Would perform update:");
                _logger.LogInfo($"  - Backup current: {options.Destination} -> {GetBackupPath(options)}");
                _logger.LogInfo($"  - Deploy new: {newBuildPath} -> {options.Destination}");
                return;
            }

            // Create backup
            string? backupPath = null;
            if (Directory.Exists(options.Destination))
            {
                backupPath = CreateBackup(options);
            }

            // Deploy new build
            DeployNewBuild(newBuildPath, options.Destination);

            // Cleanup temp
            CleanupTemp(tempFolder);

            // Success summary
            _logger.LogInfo("Update completed successfully!");
            _logger.LogInfo($"  - Destination updated: {options.Destination}");
            if (backupPath != null)
            {
                _logger.LogInfo($"  - Backup created: {backupPath}");
            }
            _logger.LogInfo($"  - Source used: {options.Source}");
        }
        catch
        {
            CleanupTemp(tempFolder);
            throw;
        }
    }

    private void ValidateOptions(UpdateOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Source))
        {
            throw new ArgumentException("Source path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(options.Destination))
        {
            throw new ArgumentException("Destination path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(options.BackupRoot))
        {
            throw new ArgumentException("Backup root path cannot be empty");
        }
    }

    private string ResolveInnerFolder(string expandedRoot, string? specifiedInnerFolder)
    {
        if (!string.IsNullOrEmpty(specifiedInnerFolder))
        {
            var path = Path.Combine(expandedRoot, specifiedInnerFolder);
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Specified inner folder not found: {specifiedInnerFolder}");
            }
            return specifiedInnerFolder;
        }

        // Auto-detect: find directories in expanded root
        var directories = Directory.GetDirectories(expandedRoot);
        var files = Directory.GetFiles(expandedRoot);

        // If there are files in root and no subdirectories, the root itself is the inner folder
        // In this case, we'll deploy the entire expanded root
        if (files.Length > 0 && directories.Length == 0)
        {
            // Return empty string to indicate the root itself
            return string.Empty;
        }

        if (directories.Length == 0)
        {
            throw new InvalidOperationException("No directories found in expanded package");
        }

        if (directories.Length == 1)
        {
            return Path.GetFileName(directories[0]);
        }

        // Multiple directories - fail and suggest
        var folderNames = directories.Select(d => Path.GetFileName(d)).ToList();
        throw new InvalidOperationException(
            $"Multiple directories found in package: {string.Join(", ", folderNames)}. " +
            "Please specify --inner-folder option.");
    }

    private void PreserveConfigFile(UpdateOptions options, string newBuildPath)
    {
        var sourceConfig = Path.Combine(options.Destination, options.ConfigFile);
        var targetConfig = Path.Combine(newBuildPath, options.ConfigFile);

        if (File.Exists(sourceConfig))
        {
            _logger.LogInfo($"Preserving config file: {options.ConfigFile}");
            File.Copy(sourceConfig, targetConfig, overwrite: true);
        }
        else
        {
            if (options.RequireConfig)
            {
                throw new FileNotFoundException(
                    $"Required config file not found: {sourceConfig}. " +
                    "Set --require-config=false to allow missing config.");
            }
            _logger.LogWarn($"Config file not found in destination: {sourceConfig}. Continuing without it.");
        }
    }

    private string CreateBackup(UpdateOptions options)
    {
        if (!Directory.Exists(options.BackupRoot))
        {
            Directory.CreateDirectory(options.BackupRoot);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupName = $"{Path.GetFileName(options.Destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}_{timestamp}";
        var backupPath = Path.Combine(options.BackupRoot, backupName);

        if (Directory.Exists(backupPath))
        {
            throw new InvalidOperationException($"Backup folder already exists: {backupPath}");
        }

        _logger.LogInfo($"Creating backup: {backupPath}");
        DirectoryHelper.MoveDirectory(options.Destination, backupPath, _logger);
        
        return backupPath;
    }

    private void DeployNewBuild(string newBuildPath, string destination)
    {
        _logger.LogInfo($"Deploying new build to: {destination}");
        
        if (Directory.Exists(destination))
        {
            throw new InvalidOperationException($"Destination already exists (should have been moved to backup): {destination}");
        }

        DirectoryHelper.MoveDirectory(newBuildPath, destination, _logger);
    }

    private string GetBackupPath(UpdateOptions options)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupName = $"{Path.GetFileName(options.Destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}_{timestamp}";
        return Path.Combine(options.BackupRoot, backupName);
    }

    private void CleanupTemp(string tempFolder)
    {
        try
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"Failed to cleanup temp folder: {ex.Message}");
        }
    }
}


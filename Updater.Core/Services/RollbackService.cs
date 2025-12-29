using System.IO;
using Updater.Core.Models;

namespace Updater.Core.Services;

public class RollbackService
{
    private readonly ILogger _logger;
    private readonly FileLockDetector _lockDetector;

    public RollbackService(ILogger logger, FileLockDetector lockDetector)
    {
        _logger = logger;
        _lockDetector = lockDetector;
    }

    public void Rollback(RollbackOptions options)
    {
        _logger.LogInfo("Starting rollback process...");

        ValidateOptions(options);

        // Check if backup root exists and has backups
        if (!Directory.Exists(options.BackupRoot))
        {
            throw new InvalidOperationException(
                $"Backup root directory does not exist: {options.BackupRoot}\n" +
                "No backups are available. You need to perform at least one update before you can rollback.\n" +
                "Use 'list-versions' command to see available backups.");
        }

        // Determine which backup to use
        string backupPath;
        if (options.UseLast)
        {
            backupPath = FindLastBackup(options.BackupRoot);
            if (string.IsNullOrEmpty(backupPath))
            {
                throw new InvalidOperationException(
                    $"No backups found in backup root: {options.BackupRoot}\n" +
                    "The backup directory exists but is empty. You need to perform at least one update before you can rollback.\n" +
                    "Use 'list-versions' command to see available backups.");
            }
            _logger.LogInfo($"Using last backup: {backupPath}");
        }
        else if (!string.IsNullOrEmpty(options.ToVersion))
        {
            backupPath = Path.Combine(options.BackupRoot, options.ToVersion);
            if (!Directory.Exists(backupPath))
            {
                // Try to provide helpful suggestions
                var availableBackups = Directory.GetDirectories(options.BackupRoot)
                    .Select(d => Path.GetFileName(d))
                    .ToList();
                
                var errorMsg = $"Backup version not found: {options.ToVersion}\n" +
                              $"Backup root: {options.BackupRoot}";
                
                if (availableBackups.Count > 0)
                {
                    errorMsg += $"\n\nAvailable backups:\n  - {string.Join("\n  - ", availableBackups.Take(10))}";
                    if (availableBackups.Count > 10)
                    {
                        errorMsg += $"\n  ... and {availableBackups.Count - 10} more";
                    }
                    errorMsg += "\n\nUse 'list-versions' command to see all available backups.";
                }
                else
                {
                    errorMsg += "\n\nNo backups found in the backup root.";
                }
                
                throw new DirectoryNotFoundException(errorMsg);
            }
            _logger.LogInfo($"Using specified backup: {backupPath}");
        }
        else
        {
            throw new ArgumentException("Either --last or --to <version> must be specified");
        }

        // Detect locked files in destination
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

        // Move current destination to backup (so rollback is reversible)
        string? currentBackupPath = null;
        if (Directory.Exists(options.Destination))
        {
            currentBackupPath = CreateBackupFromCurrent(options);
        }

        // Move selected backup to destination
        _logger.LogInfo($"Restoring backup to destination: {options.Destination}");
        DirectoryHelper.MoveDirectory(backupPath, options.Destination, _logger);

        _logger.LogInfo("Rollback completed successfully!");
        _logger.LogInfo($"  - Destination restored: {options.Destination}");
        _logger.LogInfo($"  - Backup used: {backupPath}");
        if (currentBackupPath != null)
        {
            _logger.LogInfo($"  - Previous version backed up to: {currentBackupPath}");
        }
    }

    private void ValidateOptions(RollbackOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Destination))
        {
            throw new ArgumentException("Destination path cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(options.BackupRoot))
        {
            throw new ArgumentException("Backup root path cannot be empty");
        }

        // Note: We don't check if backup root exists here anymore
        // We check it in Rollback() method to provide better error messages
    }

    private string FindLastBackup(string backupRoot)
    {
        var backups = Directory.GetDirectories(backupRoot)
            .Select(d => new
            {
                Path = d,
                Name = Path.GetFileName(d),
                LastWriteTime = Directory.GetLastWriteTime(d)
            })
            .OrderByDescending(b => b.LastWriteTime)
            .ToList();

        return backups.FirstOrDefault()?.Path ?? string.Empty;
    }

    private string CreateBackupFromCurrent(RollbackOptions options)
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

        _logger.LogInfo($"Backing up current version before rollback: {backupPath}");
        DirectoryHelper.MoveDirectory(options.Destination, backupPath, _logger);
        
        return backupPath;
    }
}


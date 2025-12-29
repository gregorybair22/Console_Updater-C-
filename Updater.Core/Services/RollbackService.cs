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

        // Determine which backup to use
        string backupPath;
        if (options.UseLast)
        {
            backupPath = FindLastBackup(options.BackupRoot);
            if (string.IsNullOrEmpty(backupPath))
            {
                throw new InvalidOperationException("No backups found in backup root");
            }
            _logger.LogInfo($"Using last backup: {backupPath}");
        }
        else if (!string.IsNullOrEmpty(options.ToVersion))
        {
            backupPath = Path.Combine(options.BackupRoot, options.ToVersion);
            if (!Directory.Exists(backupPath))
            {
                throw new DirectoryNotFoundException($"Backup version not found: {backupPath}");
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
        Directory.Move(backupPath, options.Destination);

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

        if (!Directory.Exists(options.BackupRoot))
        {
            throw new DirectoryNotFoundException($"Backup root not found: {options.BackupRoot}");
        }
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
        Directory.Move(options.Destination, backupPath);
        
        return backupPath;
    }
}


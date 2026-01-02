# Updater.Cli - Console Updater with Versioning, Locked-File Detection, and Rollback

A robust .NET console application for updating folder-based installations on Windows. Supports ZIP, RAR, and folder sources with automatic backup creation, locked-file detection, and rollback capabilities.

## Build Instructions

### Prerequisites
- .NET 9.0 SDK or later
- For RAR extraction: WinRAR installed (WinRAR.exe must be available in PATH or specified via `--winrar-path`)

### Building
```bash
dotnet build
```

### Running
```bash
dotnet run --project Updater.Cli
```

Or build and run the executable:
```bash
dotnet publish -c Release
# Executable will be in: Updater.Cli/bin/Release/net9.0/publish/Updater.Cli.exe
```

## Commands

### `update`
Deploy a package to the destination folder, creating a backup first.

**High-level flow:**
1. Validate inputs
2. Prepare a unique temp folder
3. Expand the source (.zip/.rar/folder) into temp
4. Resolve the "inner folder" to deploy
5. Copy config from current installation into the new build (preserve config)
6. Detect locked files in destination (fail before making changes)
7. Move current installation into backup folder
8. Move new build into destination
9. Cleanup temp

### `rollback`
Restore destination folder from backups.

**Supported rollback modes:**
- `--last`: rollback to most recent backup
- `--to <versionId>`: rollback to a specific backup version

**Flow:**
1. Validate destination and backup presence
2. Detect locked files in destination (fail before changes)
3. Move current destination to a new backup (so rollback is also reversible)
4. Move selected backup into destination

### `list-versions`
List available backup versions found in `--backup-root`, sorted by date. Indicates which entry would be used by `rollback --last`.

### `help`, `--help`, `?`
Print full help. Also triggered automatically on argument parsing errors.

## Options

### Global Options
- `--help`, `-?`, `?` - Show help for the command

### Update Command Options
- `--source <path>` - Source package (ZIP, RAR, or folder)
  - Default: `.\net7.0-windows.rar`
- `--dest <path>` - Destination folder to update
  - Default: `..\maquinasdispensadorasnuevosoftware`
- `--backup-root <path>` - Root folder for backups
  - Default: `.\secur`
- `--config-file <name>` - Config file name to preserve
  - Default: `appsettings.json`
- `--inner-folder <name>` - Inner folder to deploy (auto-detect if not specified)
- `--preserve-config <true|false>` - Preserve config file from destination
  - Default: `true`
- `--require-config <true|false>` - Fail if config file is missing
  - Default: `false`
- `--dry-run` - Show what would be done without making changes
- `--winrar-path <path>` - Path to WinRAR.exe (for RAR extraction)

### Rollback Command Options
- `--dest <path>` - Destination folder to restore
  - Default: `..\maquinasdispensadorasnuevosoftware`
- `--backup-root <path>` - Root folder for backups
  - Default: `.\secur`
- `--last` - Rollback to most recent backup
- `--to <versionId>` - Rollback to specific backup version

### List-Versions Command Options
- `--backup-root <path>` - Root folder for backups
  - Default: `.\secur`

## Defaults

When running commands without options, the following defaults are used:

- `--source`: `.\net7.0-windows.rar`
- `--dest`: `..\maquinasdispensadorasnuevosoftware`
- `--backup-root`: `.\secur`
- `--config-file`: `appsettings.json`
- `--inner-folder`: auto-detect (if exactly one directory found in package)

## Exit Codes

- `0` - Success
- `1` - Invalid arguments / validation error
- `2` - IO error / locked files
- `3` - Extraction error (zip/rar/folder expansion)
- `4` - Deploy/backup failure
- `5` - Rollback failure

## Examples

### Help
```bash
Updater.Cli ?
Updater.Cli --help
Updater.Cli update --help
```

### Update
```bash
# Update with defaults
Updater.Cli update

# Update from ZIP file
Updater.Cli update --source update.zip

# Update from RAR with specific inner folder
Updater.Cli update --source C:\releases\build.rar --inner-folder net7.0-windows

# Update from folder source
Updater.Cli update --source C:\releases\staging\ --dest C:\App\Install

# Dry run to see what would happen
Updater.Cli update --dry-run --source update.zip

# Update with custom paths
Updater.Cli update --source update.zip --dest C:\MyApp --backup-root C:\Backups
```

### List Versions
```bash
# List all backup versions
Updater.Cli list-versions

# List versions from custom backup root
Updater.Cli list-versions --backup-root C:\Backups
```

### Rollback
```bash
# Rollback to most recent backup
Updater.Cli rollback --last

# Rollback to specific version
Updater.Cli rollback --to maquinasdispensadorasnuevosoftware_20251225_221501

# Rollback with custom paths
Updater.Cli rollback --last --dest C:\MyApp --backup-root C:\Backups
```

## Inner Folder Resolution

The updater automatically detects the inner folder to deploy:

1. If `--inner-folder` is specified, that exact folder must exist inside the expanded package.
2. If not specified, auto-detection:
   - If the expanded root contains exactly one directory, that directory is deployed.
   - If it contains multiple directories, the tool fails and prints the detected folder names, suggesting `--inner-folder`.
   - If it contains files directly (no subdirectories), the root itself is deployed.

## Config Preservation

By default, the tool preserves `appsettings.json` from the current installation:

- `--preserve-config true` (default): Copy config file from destination to new build before deployment.
- `--preserve-config false`: Do not preserve config file.
- `--require-config true`: Fail if config file is missing in destination.
- `--require-config false` (default): Log a warning and continue if config is missing.

## Locked File Detection

Before moving or replacing the destination folder, the tool detects files that cannot be accessed (likely opened by another process):

- Scans all files in `--dest` recursively
- Attempts to open each file with exclusive access (`FileShare.None`)
- If any file cannot be opened, it's treated as locked
- Prints a clear list of locked file paths
- Exits with code `2`
- Does not change anything (no backup, no deployment) if locked files exist

## Archive Support

### ZIP
Uses built-in .NET APIs (`System.IO.Compression`). No external dependencies required.

### RAR
RAR is not supported by .NET out of the box. The tool uses **WinRAR CLI**:

**Prerequisites:**
- WinRAR must be installed
- `WinRAR.exe` must be available in one of:
  - PATH environment variable
  - `C:\Program Files\WinRAR\WinRAR.exe`
  - `C:\Program Files (x86)\WinRAR\WinRAR.exe`
  - Or specify via `--winrar-path` option

**Installation:**
Download and install WinRAR from: https://www.winrar.com/

The tool will fail gracefully with a clear error if RAR extraction is requested but WinRAR is not available.

## Safety Features

- **Never overwrites existing backup folders** - Each backup gets a unique timestamp-based name
- **Avoids partial states** - If deployment fails, provides clear, deterministic state and logs
- **Always cleans temp folders** when possible
- **Non-interactive by default** - Runs without user prompts (except printing help/errors)
- **Reversible rollback** - When rolling back, the current version is backed up first, so rollback can be undone

## Logging

Console output includes clear, actionable messages with levels:
- `[INFO]` - Informational messages
- `[WARN]` - Warnings (non-fatal)
- `[ERROR]` - Errors (fatal)

On success, a summary is printed:
- Destination updated
- Backup folder created (version id)
- Source used

## Error Handling

The tool provides clear error messages:
- On argument errors: prints one-line error, then full help, exits with code 1
- On locked files: lists all locked files, exits with code 2
- On extraction errors: clear error message, exits with code 3
- On deployment errors: clear error message, exits with code 4
- On rollback errors: clear error message, exits with code 5

## Project Structure

```
Updater/
├── Updater.Core/          # Core library with business logic
│   ├── Models/            # Option models
│   └── Services/          # Core services (Update, Rollback, etc.)
├── Updater.Cli/           # Console application
│   ├── CommandParser.cs   # Command-line argument parsing
│   ├── HelpPrinter.cs     # Help output
│   └── Program.cs         # Entry point
└── README.md              # This file
```

## Notes

- Assumes Windows environment
- Designed to be simple, deterministic, and easy to run from Task Scheduler
- All paths can be relative or absolute
- Backup folder names use format: `{destination-folder-name}_{yyyyMMdd_HHmmss}`


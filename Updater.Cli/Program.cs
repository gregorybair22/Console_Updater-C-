using Updater.Core.Services;
using Updater.Cli;

var logger = new ConsoleLogger();
var extractor = new ArchiveExtractor(logger);
var lockDetector = new FileLockDetector(logger);
var updateService = new UpdateService(logger, extractor, lockDetector);
var rollbackService = new RollbackService(logger, lockDetector);
var versionLister = new VersionLister(logger);
var parser = new CommandParser();
var helpPrinter = new HelpPrinter();

try
{
    ParsedCommand? command;
    
    try
    {
        command = parser.Parse(args);
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"[ERROR] {ex.Message}");
        Console.WriteLine();
        helpPrinter.PrintHelp();
        return 1;
    }

    if (command == null)
    {
        helpPrinter.PrintHelp();
        return 0;
    }

    if (command.Command == "help")
    {
        helpPrinter.PrintHelp(command.SubCommand);
        return 0;
    }

    try
    {
        switch (command.Command)
        {
            case "update":
                if (command.UpdateOptions == null)
                {
                    throw new InvalidOperationException("Update options not parsed correctly");
                }
                updateService.Update(command.UpdateOptions);
                return 0;

            case "rollback":
                if (command.RollbackOptions == null)
                {
                    throw new InvalidOperationException("Rollback options not parsed correctly");
                }
                rollbackService.Rollback(command.RollbackOptions);
                return 0;

            case "list-versions":
                if (command.ListVersionsOptions == null)
                {
                    throw new InvalidOperationException("ListVersions options not parsed correctly");
                }
                versionLister.ListVersions(command.ListVersionsOptions);
                return 0;

            default:
                Console.WriteLine($"[ERROR] Unknown command: {command.Command}");
                Console.WriteLine();
                helpPrinter.PrintHelp();
                return 1;
        }
    }
    catch (FileNotFoundException ex)
    {
        logger.LogError(ex.Message);
        return 2;
    }
    catch (DirectoryNotFoundException ex)
    {
        logger.LogError(ex.Message);
        return 2;
    }
    catch (IOException ex)
    {
        logger.LogError(ex.Message);
        return 2;
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("locked") || ex.Message.Contains("Locked"))
    {
        logger.LogError(ex.Message);
        return 2;
    }
    catch (NotSupportedException ex) when (ex.Message.Contains("archive") || ex.Message.Contains("extraction"))
    {
        logger.LogError(ex.Message);
        return 3;
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("extraction") || ex.Message.Contains("7z"))
    {
        logger.LogError(ex.Message);
        return 3;
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("backup") || ex.Message.Contains("deploy"))
    {
        logger.LogError(ex.Message);
        return 4;
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("rollback"))
    {
        logger.LogError(ex.Message);
        return 5;
    }
    catch (Exception ex)
    {
        logger.LogError($"Unexpected error: {ex.Message}");
        return 1;
    }
}
catch (Exception ex)
{
    logger.LogError($"Fatal error: {ex.Message}");
    return 1;
}

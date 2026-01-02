namespace Updater.Core.Models;

public class UpdateOptions
{
    public string Source { get; set; } = @".\net7.0-windows.rar";
    public string Destination { get; set; } = @"..\maquinasdispensadorasnuevosoftware";
    public string BackupRoot { get; set; } = @".\secur";
    public string ConfigFile { get; set; } = "appsettings.json";
    public string? InnerFolder { get; set; }
    public bool PreserveConfig { get; set; } = true;
    public bool RequireConfig { get; set; } = false;
    public bool DryRun { get; set; } = false;
    public string? WinRarPath { get; set; }
}


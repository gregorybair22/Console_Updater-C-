namespace Updater.Core.Models;

public class RollbackOptions
{
    public string Destination { get; set; } = @"..\maquinasdispensadorasnuevosoftware";
    public string BackupRoot { get; set; } = @".\secur";
    public bool UseLast { get; set; } = false;
    public string? ToVersion { get; set; }
}


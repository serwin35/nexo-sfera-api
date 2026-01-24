namespace NexoSferaApi.Configuration;

public class SferaSettings
{
    public string Server { get; set; } = "(local)\\INSERTNEXO";
    public string Database { get; set; } = "Nexo_demo_1";
    public string? SqlLogin { get; set; }
    public string? SqlPassword { get; set; }
    public bool UseWindowsAuth { get; set; } = true;
    public string NexoLogin { get; set; } = "Szef";
    public string NexoPassword { get; set; } = "robocze";
    public string Product { get; set; } = "Subiekt"; // Subiekt, Rachmistrz, Rewizor, Gratyfikant

    // Context settings - required for document operations
    public string? DefaultWarehouse { get; set; } // e.g., "MAG"
    public string? DefaultBranch { get; set; } // e.g., "CENTRALA"
    public string? DefaultCashRegister { get; set; } // e.g., "CENTR"
}

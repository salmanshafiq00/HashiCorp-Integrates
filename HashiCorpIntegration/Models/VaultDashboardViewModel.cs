namespace HashiCorpIntegration.Models;

// ViewModels
public class VaultDashboardViewModel
{
    public bool UseStaticCredentials { get; set; }

    // Dynamic credential properties
    public CurrentLeaseViewModel? CurrentLease { get; set; }
    public List<LeaseViewModel> AllLeases { get; set; } = [];

    // Static credential properties
    public StaticCredentialViewModel? StaticCredential { get; set; }
    public List<RotationHistoryViewModel> RotationHistory { get; set; } = [];

    // Database connection properties
    public bool DatabaseConnectionSuccess { get; set; }
    public string? DatabaseError { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCount { get; set; }

    // General properties
    public string? Error { get; set; }
}

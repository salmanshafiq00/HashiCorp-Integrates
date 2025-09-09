namespace HashiCorpIntegration.Models;

// ViewModels
public class VaultDashboardViewModel
{
    public CurrentLeaseViewModel? CurrentLease { get; set; }
    public List<LeaseViewModel> AllLeases { get; set; } = new();
    public bool DatabaseConnectionSuccess { get; set; }
    public string? DatabaseError { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCount { get; set; }
    public string? Error { get; set; }
}

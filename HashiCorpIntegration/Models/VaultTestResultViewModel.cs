namespace HashiCorpIntegration.Models;

// Keep existing ViewModels for compatibility
public class VaultTestResultViewModel
{
    public bool VaultConnectionSuccess { get; set; }
    public string? VaultConnectionString { get; set; }
    public string? VaultError { get; set; }
    public bool DatabaseConnectionSuccess { get; set; }
    public string? DatabaseError { get; set; }
}

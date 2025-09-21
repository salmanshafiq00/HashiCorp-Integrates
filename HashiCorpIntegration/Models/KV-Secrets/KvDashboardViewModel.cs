// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvDashboardViewModel
{
    public List<KvSecretViewModel> Secrets { get; set; } = [];
    public List<string> SecretPaths { get; set; } = [];
    public string? Error { get; set; }
    public string CurrentPath { get; set; } = "";
    public bool VaultConnectionSuccess { get; set; }
    public string? VaultError { get; set; }
}

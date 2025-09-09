namespace HashiCorpIntegration.Models;

public class CredentialInfoViewModel
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Username { get; set; }
    public bool IsVaultGenerated { get; set; }
    public DateTime RetrievedAt { get; set; }
    public string LeaseId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public TimeSpan TimeRemaining { get; set; }
}
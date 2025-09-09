namespace HashiCorpIntegration.Models;

public class CredentialInfoViewModel
{
    public bool Success { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsVaultGenerated { get; set; }
    public DateTime RetrievedAt { get; set; }
    public string? Error { get; set; }

    // For dynamic credentials
    public string LeaseId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public TimeSpan TimeRemaining { get; set; }

    // For static credentials
    public DateTime LastRotated { get; set; }
    public DateTime NextRotation { get; set; }
    public TimeSpan RotationPeriod { get; set; }
    public bool IsStatic { get; set; }
}
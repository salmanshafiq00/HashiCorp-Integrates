namespace HashiCorpIntegration.Vault;

public class LeaseInfo
{
    public string LeaseId { get; set; } = string.Empty;
    public TimeSpan LeaseDuration { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsCurrentlyUsed { get; set; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public TimeSpan TimeRemaining => ExpiresAt > DateTime.UtcNow ? ExpiresAt - DateTime.UtcNow : TimeSpan.Zero;
}
namespace HashiCorpIntegration.src.Models;

public class CurrentLeaseViewModel
{
    public string LeaseId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public TimeSpan TimeRemaining { get; set; }
    public bool IsExpired { get; set; }
    public TimeSpan LeaseDuration { get; set; }
}

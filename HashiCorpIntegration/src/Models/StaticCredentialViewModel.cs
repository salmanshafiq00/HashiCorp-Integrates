namespace HashiCorpIntegration.src.Models;

public class StaticCredentialViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime LastRotated { get; set; }
    public TimeSpan RotationPeriod { get; set; }
    public DateTime NextRotation { get; set; }
    public DateTime RetrievedAt { get; set; }
    public bool IsExpired { get; set; }
    public TimeSpan TimeUntilRotation { get; set; }
}
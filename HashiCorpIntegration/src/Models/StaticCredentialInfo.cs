namespace HashiCorpIntegration.src.Models;

public class StaticCredentialInfo
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime LastRotated { get; set; }
    public TimeSpan RotationPeriod { get; set; }
    public DateTime NextRotation { get; set; }
    public DateTime RetrievedAt { get; set; }
    public bool IsExpired => DateTime.UtcNow > NextRotation;
    public TimeSpan TimeUntilRotation => NextRotation > DateTime.UtcNow ? NextRotation - DateTime.UtcNow : TimeSpan.Zero;
}

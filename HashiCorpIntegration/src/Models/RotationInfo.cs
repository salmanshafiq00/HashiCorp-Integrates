namespace HashiCorpIntegration.src.Models;

public class RotationInfo
{
    public DateTime RotatedAt { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
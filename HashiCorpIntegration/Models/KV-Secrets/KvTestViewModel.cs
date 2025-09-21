// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvTestViewModel
{
    public string TestPath { get; set; } = string.Empty;
    public string TestKey { get; set; } = string.Empty;
    public string? RetrievedValue { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime TestExecutedAt { get; set; }
    public TimeSpan ResponseTime { get; set; }

}
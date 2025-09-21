// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvSecretDetailViewModel
{
    public string Path { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = [];
    public DateTime RetrievedAt { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

}

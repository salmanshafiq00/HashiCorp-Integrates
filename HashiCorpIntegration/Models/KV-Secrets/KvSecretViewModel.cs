// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvSecretViewModel
{
    public string Path { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = [];
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public int KeyCount => Data.Count;

}

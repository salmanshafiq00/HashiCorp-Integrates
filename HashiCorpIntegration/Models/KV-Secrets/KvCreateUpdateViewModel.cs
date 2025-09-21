// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvCreateUpdateViewModel
{
    public string Path { get; set; } = "";
    public List<KvKeyValuePair> KeyValuePairs { get; set; } = [new KvKeyValuePair()];
    public bool IsUpdate { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

}

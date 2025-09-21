// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvKeyValuePair
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsEmpty => string.IsNullOrWhiteSpace(Key);
}

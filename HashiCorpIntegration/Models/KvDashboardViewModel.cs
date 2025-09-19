// Create these models in your Models folder

namespace HashiCorpIntegration.src.Models;

public class KvDashboardViewModel
{
    public List<KvSecretViewModel> Secrets { get; set; } = [];
    public List<string> SecretPaths { get; set; } = [];
    public string? Error { get; set; }
    public string CurrentPath { get; set; } = "";
    public bool VaultConnectionSuccess { get; set; }
    public string? VaultError { get; set; }
}

public class KvSecretViewModel
{
    public string Path { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = [];
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public int KeyCount => Data.Count;

}

public class KvSecretDetailViewModel
{
    public string Path { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = [];
    public DateTime RetrievedAt { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

}

public class KvCreateUpdateViewModel
{
    public string Path { get; set; } = "";
    public List<KvKeyValuePair> KeyValuePairs { get; set; } = [new KvKeyValuePair()];
    public bool IsUpdate { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

}

public class KvKeyValuePair
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public bool IsEmpty => string.IsNullOrWhiteSpace(Key);
}

public class KvTestViewModel
{
    public string TestPath { get; set; } 
    public string TestKey { get; set; } 
    public string? RetrievedValue { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime TestExecutedAt { get; set; }
    public TimeSpan ResponseTime { get; set; }

}
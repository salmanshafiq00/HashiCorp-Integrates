namespace HashiCorpIntegration.Vault;

public class VaultSettings
{
    public const string SectionName = "Vault";
    public string VaultUrl { get; set; } = string.Empty;
    public string VaultToken { get; set; } = string.Empty;
    public string DatabaseRole { get; set; } = string.Empty;
    public string DatabaseServer { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public int CacheExpirationMinutes { get; set; } = 30;

    // Keep these for future AppRole setup
    public string RoleId { get; set; } = string.Empty;
    public string SecretId { get; set; } = string.Empty;
    public string SqlSecretPath { get; set; } = string.Empty;
}
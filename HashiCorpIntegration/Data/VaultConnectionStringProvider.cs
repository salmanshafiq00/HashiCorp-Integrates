using HashiCorpIntegration.Vault;

namespace HashiCorpIntegration.Data;

public class VaultConnectionStringProvider : IConnectionStringProvider
{
    private readonly IVaultService _vaultService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VaultConnectionStringProvider> _logger;

    public VaultConnectionStringProvider(IVaultService vaultService,
                                       IConfiguration configuration,
                                       ILogger<VaultConnectionStringProvider> logger)
    {
        _vaultService = vaultService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetConnectionStringAsync()
    {
        try
        {
            var connectionString = await _vaultService.GetSqlConnectionStringAsync();
            _logger.LogDebug("Retrieved connection string from Vault");
            return connectionString;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve connection string from Vault, using fallback");

            var fallbackConnectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(fallbackConnectionString))
            {
                throw new InvalidOperationException("No connection string available from Vault or configuration", ex);
            }

            return fallbackConnectionString;
        }
    }
}

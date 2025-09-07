using HashiCorpIntegration.Data;
using HashiCorpIntegration.Vault;
using Microsoft.Extensions.Options;

namespace HashiCorpIntegration.Jobs;

public class VaultCredentialRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VaultCredentialRefreshService> _logger;
    private readonly VaultSettings _vaultSettings;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(30); // Refresh every 30 minutes

    public VaultCredentialRefreshService(
        IServiceProvider serviceProvider,
        ILogger<VaultCredentialRefreshService> logger,
        IOptions<VaultSettings> vaultSettings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _vaultSettings = vaultSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionStringProvider = scope.ServiceProvider.GetRequiredService<IConnectionStringProvider>();

                // This will refresh the cached connection string
                await connectionStringProvider.GetConnectionStringAsync();

                _logger.LogInformation("Vault credentials refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh Vault credentials");
            }

            await Task.Delay(_refreshInterval, stoppingToken);
        }
    }
}
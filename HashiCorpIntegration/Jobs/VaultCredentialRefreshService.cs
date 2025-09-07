using HashiCorpIntegration.Vault;

namespace HashiCorpIntegration.Jobs;

public class VaultCredentialRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VaultCredentialRefreshService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(30);

    public VaultCredentialRefreshService(
        IServiceProvider serviceProvider,
        ILogger<VaultCredentialRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var vaultService = scope.ServiceProvider.GetRequiredService<IVaultService>();

                // Invalidate cache to force refresh on next request
                vaultService.InvalidateConnectionCache();

                // Preload new credentials
                await vaultService.GetSqlConnectionStringAsync();

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
using HashiCorpIntegration.Vault;

namespace HashiCorpIntegration.Jobs;

public class VaultCredentialRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VaultCredentialRefreshService> _logger;

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

                var leaseInfo = vaultService.GetCurrentLeaseInfo();

                if (leaseInfo != null)
                {
                    var timeUntilExpiry = leaseInfo.ExpiresAt - DateTime.UtcNow;

                    // If less than 10 minutes until expiry, refresh now
                    if (timeUntilExpiry.TotalMinutes <= 10)
                    {
                        _logger.LogInformation("Credentials expire in {Minutes} minutes, refreshing now",
                            timeUntilExpiry.TotalMinutes);

                        vaultService.InvalidateConnectionCache();
                        await vaultService.GetSqlConnectionStringAsync();
                        _logger.LogInformation("Vault credentials refreshed successfully");
                    }
                    else
                    {
                        _logger.LogDebug("Credentials valid for {Minutes} more minutes", timeUntilExpiry.TotalMinutes);
                    }

                    // Wait until 10 minutes before expiry, or at least 5 minutes
                    var waitTime = timeUntilExpiry.Subtract(TimeSpan.FromMinutes(10));
                    if (waitTime < TimeSpan.FromMinutes(5))
                    {
                        waitTime = TimeSpan.FromMinutes(5);
                    }

                    await Task.Delay(waitTime, stoppingToken);
                }
                else
                {
                    // No lease info available, try to get fresh credentials
                    _logger.LogInformation("No lease info available, attempting to refresh credentials");
                    await vaultService.GetSqlConnectionStringAsync();
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh Vault credentials, retrying in 5 minutes");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
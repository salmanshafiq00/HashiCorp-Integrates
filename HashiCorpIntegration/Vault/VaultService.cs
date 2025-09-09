using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

namespace HashiCorpIntegration.Vault;

public class VaultService(
    IOptions<VaultSettings> vaultSettings,
    IMemoryCache memoryCache,
    ILogger<VaultService> logger) : IVaultService
{
    private readonly VaultSettings _vaultSettings = vaultSettings.Value;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private const string CONNECTION_CACHE_KEY = "vault_connection_string";
    private const string LEASE_INFO_CACHE_KEY = "vault_lease_info";
    private const string ALL_LEASES_CACHE_KEY = "vault_all_leases";
    private VaultClient? _vaultClient;

    private VaultClient GetVaultClient()
    {
        if (_vaultClient == null)
        {
            try
            {
                var vaultClientSettings = new VaultClientSettings(_vaultSettings.VaultUrl,
                    new TokenAuthMethodInfo(_vaultSettings.VaultToken));
                _vaultClient = new VaultClient(vaultClientSettings);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create Vault client");
                throw;
            }
        }
        return _vaultClient;
    }

    public async Task<string> GetSqlConnectionStringAsync()
    {
        // Try to get from cache first
        if (_memoryCache.TryGetValue(CONNECTION_CACHE_KEY, out string? cachedConnectionString) &&
            !string.IsNullOrEmpty(cachedConnectionString))
        {
            logger.LogDebug("Using cached database connection string");
            return cachedConnectionString;
        }

        return await GetNewSqlConnectionStringAsync();
    }

    public async Task<string> GetNewSqlConnectionStringAsync()
    {
        try
        {
            var credentials = await GetVaultClient().V1.Secrets.Database
                .GetCredentialsAsync(_vaultSettings.DatabaseRole);

            var connectionString = $"Server={_vaultSettings.DatabaseServer};Database={_vaultSettings.DatabaseName};User Id={credentials.Data.Username};Password={credentials.Data.Password};TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30";

            // Clear EF Core connection pools
            SqlConnection.ClearAllPools();

            // Validate connection before caching
            if (!await ValidateConnectionAsync(connectionString))
            {
                throw new InvalidOperationException("Dynamic credentials are valid but database connection failed. Check user permissions.");
            }

            // Cache the connection string and lease info
            var leaseDuration = TimeSpan.FromSeconds(credentials.LeaseDurationSeconds);
            var cacheExpiry = DateTime.UtcNow.Add(leaseDuration).AddMinutes(-5);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = cacheExpiry,
                Priority = CacheItemPriority.High
            };

            _memoryCache.Set(CONNECTION_CACHE_KEY, connectionString, cacheOptions);

            // Store lease info for background service
            var leaseInfo = new LeaseInfo
            {
                LeaseId = credentials.LeaseId,
                LeaseDuration = leaseDuration,
                ExpiresAt = cacheExpiry,
                CreatedAt = DateTime.UtcNow,
                Username = credentials.Data.Username,
                IsCurrentlyUsed = true
            };
            _memoryCache.Set(LEASE_INFO_CACHE_KEY, leaseInfo, cacheOptions);

            // Update all leases cache
            await UpdateAllLeasesCache(leaseInfo);

            logger.LogInformation("Retrieved new database credentials. Username: {Username}, Expires at: {ExpiryTime}",
                credentials.Data.Username, cacheExpiry);
            return connectionString;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve dynamic database credentials from Vault");
            throw;
        }
    }

    public async Task RenewLeaseAsync(string leaseId, int incrementSeconds = 3600)
    {
        await GetVaultClient().V1.System.RenewLeaseAsync(leaseId, incrementSeconds);
        logger.LogInformation("Renewed lease {LeaseId} for {Seconds} seconds", leaseId, incrementSeconds);
    }

    public async Task RevokeSingleLeaseAsync(string leaseId)
    {
        try
        {
            await GetVaultClient().V1.System.RevokeLeaseAsync(leaseId);

            // If this is the current lease, invalidate cache
            var currentLease = GetCurrentLeaseInfo();
            if (currentLease?.LeaseId == leaseId)
            {
                InvalidateConnectionCache();
            }

            // Remove from all leases cache
            var allLeases = await GetAllActiveLeasesAsync();
            allLeases.RemoveAll(l => l.LeaseId == leaseId);
            _memoryCache.Set(ALL_LEASES_CACHE_KEY, allLeases, TimeSpan.FromMinutes(5));

            logger.LogInformation("Revoked lease {LeaseId}", leaseId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revoke lease {LeaseId}", leaseId);
            throw;
        }
    }

    public async Task RevokeAllLeasesAsync()
    {
        try
        {
            // Get all leases and revoke them
            var allLeases = await GetAllActiveLeasesAsync();

            foreach (var lease in allLeases.ToList())
            {
                try
                {
                    await RevokeSingleLeaseAsync(lease.LeaseId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to revoke lease {LeaseId}", lease.LeaseId);
                }
            }

            // Clear all caches
            InvalidateConnectionCache();
            _memoryCache.Remove(ALL_LEASES_CACHE_KEY);

            logger.LogInformation("Attempted to revoke all leases");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revoke all leases");
            throw;
        }
    }

    public async Task<List<LeaseInfo>> GetAllActiveLeasesAsync()
    {
        // Try cache first
        if (_memoryCache.TryGetValue(ALL_LEASES_CACHE_KEY, out List<LeaseInfo>? cachedLeases) &&
            cachedLeases != null)
        {
            // Update current lease status
            var currentLease = GetCurrentLeaseInfo();
            foreach (var lease in cachedLeases)
            {
                lease.IsCurrentlyUsed = currentLease?.LeaseId == lease.LeaseId;
            }

            return cachedLeases.Where(l => !l.IsExpired).ToList();
        }

        // For demo purposes, we'll maintain a simple list
        // In production, you might query Vault's sys/leases endpoint if available
        var leases = new List<LeaseInfo>();
        var currentLeaseInfo = GetCurrentLeaseInfo();

        if (currentLeaseInfo != null)
        {
            leases.Add(currentLeaseInfo);
        }

        _memoryCache.Set(ALL_LEASES_CACHE_KEY, leases, TimeSpan.FromMinutes(5));
        return leases;
    }

    private async Task UpdateAllLeasesCache(LeaseInfo newLease)
    {
        var allLeases = await GetAllActiveLeasesAsync();

        // Mark all other leases as not currently used
        foreach (var lease in allLeases)
        {
            lease.IsCurrentlyUsed = false;
        }

        // Add or update the new lease
        var existingLease = allLeases.FirstOrDefault(l => l.LeaseId == newLease.LeaseId);
        if (existingLease != null)
        {
            allLeases.Remove(existingLease);
        }

        allLeases.Add(newLease);

        // Remove expired leases
        allLeases.RemoveAll(l => l.IsExpired);

        _memoryCache.Set(ALL_LEASES_CACHE_KEY, allLeases, TimeSpan.FromMinutes(5));
    }

    public void InvalidateConnectionCache()
    {
        _memoryCache.Remove(CONNECTION_CACHE_KEY);
        _memoryCache.Remove(LEASE_INFO_CACHE_KEY);
        logger.LogInformation("Connection string cache invalidated");
    }

    public LeaseInfo? GetCurrentLeaseInfo()
    {
        _memoryCache.TryGetValue(LEASE_INFO_CACHE_KEY, out LeaseInfo? leaseInfo);
        return leaseInfo;
    }

    public async Task<string> GetSecretAsync(string path, string key)
    {
        var cacheKey = $"vault_secret_{path}_{key}";

        if (_memoryCache.TryGetValue(cacheKey, out string? cachedSecret) &&
            !string.IsNullOrEmpty(cachedSecret))
        {
            return cachedSecret;
        }

        try
        {
            var secret = await GetVaultClient().V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
            if (secret?.Data?.Data != null && secret.Data.Data.TryGetValue(key, out var value))
            {
                var secretValue = value.ToString();

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes),
                    Priority = CacheItemPriority.Normal
                };

                _memoryCache.Set(cacheKey, secretValue, cacheOptions);
                return secretValue;
            }
            throw new KeyNotFoundException($"Secret key '{key}' not found in path '{path}'");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve secret from Vault: {Path}/{Key}", path, key);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> GetSecretAsync(string path)
    {
        var secret = await GetVaultClient().V1.Secrets.KeyValue.V2.ReadSecretAsync(path);
        if (secret?.Data?.Data != null)
        {
            var secrets = new Dictionary<string, object>(secret.Data.Data);
            return secrets;
        }
        throw new InvalidOperationException($"No secrets found in path '{path}'");
    }

    private async Task<bool> ValidateConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
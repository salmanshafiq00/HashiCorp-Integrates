using HashiCorpIntegration.Models;
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

    // Dynamic credential cache keys
    private const string CONNECTION_CACHE_KEY = "vault_connection_string";
    private const string LEASE_INFO_CACHE_KEY = "vault_lease_info";
    private const string ALL_LEASES_CACHE_KEY = "vault_all_leases";

    // Static credential cache keys
    private const string STATIC_CONNECTION_CACHE_KEY = "vault_static_connection_string";
    private const string STATIC_CREDENTIAL_INFO_CACHE_KEY = "vault_static_credential_info";

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

    #region Dynamic Credentials (existing methods)

    public async Task<string> GetSqlConnectionStringAsync()
    {
        if (_vaultSettings.UseStaticCredentials)
        {
            return await GetStaticConnectionStringAsync();
        }

        // Try to get from cache first
        if (_memoryCache.TryGetValue(CONNECTION_CACHE_KEY, out string? cachedConnectionString) &&
            !string.IsNullOrEmpty(cachedConnectionString))
        {
            logger.LogDebug("Using cached dynamic database connection string");
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

            SqlConnection.ClearAllPools();

            if (!await ValidateConnectionAsync(connectionString))
            {
                throw new InvalidOperationException("Dynamic credentials are valid but database connection failed. Check user permissions.");
            }

            var leaseDuration = TimeSpan.FromSeconds(credentials.LeaseDurationSeconds);
            var cacheExpiry = DateTime.UtcNow.Add(leaseDuration).AddMinutes(-5);

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = cacheExpiry,
                Priority = CacheItemPriority.High
            };

            _memoryCache.Set(CONNECTION_CACHE_KEY, connectionString, cacheOptions);

            var leaseInfo = new LeaseInfo
            {
                LeaseId = credentials.LeaseId,
                LeaseDuration = leaseDuration,
                ExpiresAt = cacheExpiry,
                CreatedAt = DateTime.UtcNow,
                Username = credentials.Data.Username,
                Password = credentials.Data.Password,
                IsCurrentlyUsed = true
            };
            _memoryCache.Set(LEASE_INFO_CACHE_KEY, leaseInfo, cacheOptions);

            await UpdateAllLeasesCache(leaseInfo);

            logger.LogInformation("Retrieved new dynamic database credentials. Username: {Username}, Expires at: {ExpiryTime}",
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

            var currentLease = GetCurrentLeaseInfo();
            if (currentLease?.LeaseId == leaseId)
            {
                InvalidateConnectionCache();
            }

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
        if (_memoryCache.TryGetValue(ALL_LEASES_CACHE_KEY, out List<LeaseInfo>? cachedLeases) &&
            cachedLeases != null)
        {
            var currentLease = GetCurrentLeaseInfo();
            foreach (var lease in cachedLeases)
            {
                lease.IsCurrentlyUsed = currentLease?.LeaseId == lease.LeaseId;
            }

            return cachedLeases.Where(l => !l.IsExpired).ToList();
        }

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

        foreach (var lease in allLeases)
        {
            lease.IsCurrentlyUsed = false;
        }

        var existingLease = allLeases.FirstOrDefault(l => l.LeaseId == newLease.LeaseId);
        if (existingLease != null)
        {
            allLeases.Remove(existingLease);
        }

        allLeases.Add(newLease);
        allLeases.RemoveAll(l => l.IsExpired);

        _memoryCache.Set(ALL_LEASES_CACHE_KEY, allLeases, TimeSpan.FromMinutes(5));
    }

    public void InvalidateConnectionCache()
    {
        _memoryCache.Remove(CONNECTION_CACHE_KEY);
        _memoryCache.Remove(LEASE_INFO_CACHE_KEY);
        logger.LogInformation("Dynamic connection cache invalidated");
    }

    public LeaseInfo? GetCurrentLeaseInfo()
    {
        _memoryCache.TryGetValue(LEASE_INFO_CACHE_KEY, out LeaseInfo? leaseInfo);
        return leaseInfo;
    }

    #endregion

    #region Static Credentials (new methods)

    public async Task<string> GetStaticConnectionStringAsync()
    {
        if (_memoryCache.TryGetValue(STATIC_CONNECTION_CACHE_KEY, out string? cachedConnectionString) &&
            !string.IsNullOrEmpty(cachedConnectionString))
        {
            logger.LogDebug("Using cached static database connection string");
            return cachedConnectionString;
        }

        return await GetNewStaticConnectionStringAsync();
    }

    private async Task<string> GetNewStaticConnectionStringAsync()
    {
        try
        {
            var credentials = await GetVaultClient().V1.Secrets.Database
                .GetStaticCredentialsAsync(_vaultSettings.StaticDatabaseRole);

            var connectionString = $"Server={_vaultSettings.DatabaseServer};Database={_vaultSettings.DatabaseName};User Id={credentials.Data.Username};Password={credentials.Data.Password};TrustServerCertificate=True;MultipleActiveResultSets=true;Connection Timeout=30";

            SqlConnection.ClearAllPools();

            if (!await ValidateConnectionAsync(connectionString))
            {
                throw new InvalidOperationException("Static credentials are valid but database connection failed. Check user permissions.");
            }

            // For static credentials, we cache for a shorter period and rely on rotation
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_vaultSettings.CacheExpirationMinutes),
                Priority = CacheItemPriority.High
            };

            _memoryCache.Set(STATIC_CONNECTION_CACHE_KEY, connectionString, cacheOptions);

            // Store static credential info
            DateTime lastRotated;

            if (!DateTime.TryParse(credentials.Data.LastVaultRotation, out lastRotated))
            {
                logger.LogWarning("Failed to parse LastVaultRotation '{Rotation}' from Vault. Using current UTC time as fallback.", credentials.Data.LastVaultRotation);
                lastRotated = DateTime.UtcNow;
            }
            double rotationPeriod;
            if (!double.TryParse(credentials.Data.RotationPeriod, out rotationPeriod) || rotationPeriod <= 0)
            {
                logger.LogWarning("Invalid RotationPeriod '{RotationPeriod}' from Vault. Using default of 24 hours.", credentials.Data.RotationPeriod);
                rotationPeriod = 86400; // Default to 24 hours
            }
            var staticInfo = new StaticCredentialInfo
            {
                Username = credentials.Data.Username,
                Password = credentials.Data.Password,
                LastRotated = lastRotated,
                RotationPeriod = TimeSpan.FromSeconds(rotationPeriod),
                RetrievedAt = DateTime.UtcNow
            };
            staticInfo.NextRotation = staticInfo.LastRotated.Add(staticInfo.RotationPeriod);


            _memoryCache.Set(STATIC_CREDENTIAL_INFO_CACHE_KEY, staticInfo, cacheOptions);

            logger.LogInformation("Retrieved static database credentials. Username: {Username}, Next rotation: {NextRotation}",
                credentials.Data.Username, staticInfo.NextRotation);
            return connectionString;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve static database credentials from Vault");
            throw;
        }
    }

    public async Task<StaticCredentialInfo?> GetStaticCredentialInfoAsync()
    {
        if (_memoryCache.TryGetValue(STATIC_CREDENTIAL_INFO_CACHE_KEY, out StaticCredentialInfo? cachedInfo) &&
            cachedInfo != null)
        {
            return cachedInfo;
        }

        // If not cached, retrieve fresh credentials
        await GetNewStaticConnectionStringAsync();
        _memoryCache.TryGetValue(STATIC_CREDENTIAL_INFO_CACHE_KEY, out cachedInfo);
        return cachedInfo;
    }

    public async Task<RotationInfo> RotateStaticCredentialsAsync()
    {
        var rotationInfo = new RotationInfo
        {
            RotatedAt = DateTime.UtcNow
        };

        try
        {
            await GetVaultClient().V1.Secrets.Database.RotateStaticCredentialsAsync(_vaultSettings.StaticDatabaseRole);

            // Clear cache to force fresh credentials on next request
            InvalidateStaticConnectionCache();

            // Get fresh credentials to update info
            var staticInfo = await GetStaticCredentialInfoAsync();
            rotationInfo.Username = staticInfo?.Username ?? "Unknown";
            rotationInfo.Success = true;

            logger.LogInformation("Successfully rotated static credentials for role: {Role}", _vaultSettings.StaticDatabaseRole);
        }
        catch (Exception ex)
        {
            rotationInfo.Success = false;
            rotationInfo.Error = ex.Message;
            logger.LogError(ex, "Failed to rotate static credentials for role: {Role}", _vaultSettings.StaticDatabaseRole);
        }

        return rotationInfo;
    }

    public void InvalidateStaticConnectionCache()
    {
        _memoryCache.Remove(STATIC_CONNECTION_CACHE_KEY);
        _memoryCache.Remove(STATIC_CREDENTIAL_INFO_CACHE_KEY);
        logger.LogInformation("Static connection cache invalidated");
    }

    #endregion

    #region Common Methods

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
            return new Dictionary<string, object>(secret.Data.Data);
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

    #endregion
}
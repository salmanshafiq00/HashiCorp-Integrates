using Microsoft.AspNetCore.Mvc;
using HashiCorpIntegration.Data;
using HashiCorpIntegration.Vault;
using Microsoft.EntityFrameworkCore;
using HashiCorpIntegration.Models;
using Microsoft.Extensions.Options;

namespace HashiCorpIntegration.Controllers;

public class TestVaultController(
    IVaultService vaultService,
    IServiceProvider serviceProvider,
    IOptions<VaultSettings> vaultSettings,
    ILogger<TestVaultController> logger) : Controller
{
    private readonly VaultSettings _vaultSettings = vaultSettings.Value;

    public async Task<IActionResult> Index()
    {
        var model = new VaultDashboardViewModel
        {
            UseStaticCredentials = _vaultSettings.UseStaticCredentials
        };

        if (_vaultSettings.UseStaticCredentials)
        {
            await LoadStaticCredentialData(model);
        }
        else
        {
            await LoadDynamicCredentialData(model);
        }

        await TestDatabaseConnection(model);

        return View(model);
    }

    private async Task LoadDynamicCredentialData(VaultDashboardViewModel model)
    {
        // Get current lease info (don't create new one)
        var currentLease = vaultService.GetCurrentLeaseInfo();
        if (currentLease != null)
        {
            model.CurrentLease = new CurrentLeaseViewModel
            {
                LeaseId = currentLease.LeaseId,
                Username = currentLease.Username,
                Password = currentLease.Password,
                CreatedAt = currentLease.CreatedAt,
                ExpiresAt = currentLease.ExpiresAt,
                TimeRemaining = currentLease.TimeRemaining,
                IsExpired = currentLease.IsExpired,
                LeaseDuration = currentLease.LeaseDuration
            };
        }

        // Get all active leases
        try
        {
            var allLeases = await vaultService.GetAllActiveLeasesAsync();
            model.AllLeases = allLeases.Select(l => new LeaseViewModel
            {
                LeaseId = l.LeaseId,
                Username = l.Username,
                Password = l.Password,
                CreatedAt = l.CreatedAt,
                ExpiresAt = l.ExpiresAt,
                TimeRemaining = l.TimeRemaining,
                IsExpired = l.IsExpired,
                IsCurrentlyUsed = l.IsCurrentlyUsed,
                LeaseDuration = l.LeaseDuration
            }).ToList();
        }
        catch (Exception ex)
        {
            model.Error = $"Failed to load leases: {ex.Message}";
            logger.LogError(ex, "Failed to load all leases");
        }
    }

    private async Task LoadStaticCredentialData(VaultDashboardViewModel model)
    {
        try
        {
            var staticInfo = await vaultService.GetStaticCredentialInfoAsync();
            if (staticInfo != null)
            {
                model.StaticCredential = new StaticCredentialViewModel
                {
                    Username = staticInfo.Username,
                    Password = staticInfo.Password,
                    LastRotated = staticInfo.LastRotated,
                    RotationPeriod = staticInfo.RotationPeriod,
                    NextRotation = staticInfo.NextRotation,
                    RetrievedAt = staticInfo.RetrievedAt,
                    IsExpired = staticInfo.IsExpired,
                    TimeUntilRotation = staticInfo.TimeUntilRotation
                };
            }
        }
        catch (Exception ex)
        {
            model.Error = $"Failed to load static credential info: {ex.Message}";
            logger.LogError(ex, "Failed to load static credential info");
        }

        // Note: For demo purposes, you might want to load rotation history from a persistent store
        // For now, we'll show empty rotation history
        model.RotationHistory = new List<RotationHistoryViewModel>();
    }

    private async Task TestDatabaseConnection(VaultDashboardViewModel model)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            model.DatabaseConnectionSuccess = await dbContext.Database.CanConnectAsync();

            if (model.DatabaseConnectionSuccess)
            {
                model.CategoriesCount = await dbContext.Categories.CountAsync();
                model.ProductsCount = await dbContext.Products.CountAsync();
            }
        }
        catch (Exception ex)
        {
            model.DatabaseConnectionSuccess = false;
            model.DatabaseError = ex.Message;
            logger.LogError(ex, "Database connection test failed");
        }
    }

    [HttpPost]
    public async Task<IActionResult> GetNewConnection()
    {
        try
        {
            if (_vaultSettings.UseStaticCredentials)
            {
                vaultService.InvalidateStaticConnectionCache();
                await vaultService.GetStaticConnectionStringAsync();
                TempData["Success"] = "Static credentials refreshed successfully!";
            }
            else
            {
                await vaultService.GetNewSqlConnectionStringAsync();
                TempData["Success"] = "New dynamic connection credentials obtained successfully!";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to get new connection: {ex.Message}";
            logger.LogError(ex, "Failed to get new connection");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RotateStaticCredentials()
    {
        if (!_vaultSettings.UseStaticCredentials)
        {
            TempData["Error"] = "Static credentials are not enabled";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var rotationInfo = await vaultService.RotateStaticCredentialsAsync();
            if (rotationInfo.Success)
            {
                TempData["Success"] = $"Static credentials rotated successfully for user: {rotationInfo.Username}";
            }
            else
            {
                TempData["Error"] = $"Failed to rotate static credentials: {rotationInfo.Error}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to rotate static credentials: {ex.Message}";
            logger.LogError(ex, "Failed to rotate static credentials");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RevokeLease(string leaseId)
    {
        if (_vaultSettings.UseStaticCredentials)
        {
            TempData["Error"] = "Lease operations are not available for static credentials";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrEmpty(leaseId))
        {
            TempData["Error"] = "Lease ID is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await vaultService.RevokeSingleLeaseAsync(leaseId);
            TempData["Success"] = $"Lease {leaseId} revoked successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to revoke lease: {ex.Message}";
            logger.LogError(ex, "Failed to revoke lease {LeaseId}", leaseId);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RevokeAllLeases()
    {
        if (_vaultSettings.UseStaticCredentials)
        {
            TempData["Error"] = "Lease operations are not available for static credentials";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await vaultService.RevokeAllLeasesAsync();
            TempData["Success"] = "All leases revoked successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to revoke all leases: {ex.Message}";
            logger.LogError(ex, "Failed to revoke all leases");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RenewLease(string leaseId, int incrementSeconds = 3600)
    {
        if (_vaultSettings.UseStaticCredentials)
        {
            TempData["Error"] = "Lease operations are not available for static credentials";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrEmpty(leaseId))
        {
            TempData["Error"] = "Lease ID is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await vaultService.RenewLeaseAsync(leaseId, incrementSeconds);
            TempData["Success"] = $"Lease {leaseId} renewed for {incrementSeconds} seconds!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to renew lease: {ex.Message}";
            logger.LogError(ex, "Failed to renew lease {LeaseId}", leaseId);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> TestQuery()
    {
        var model = new DatabaseQueryTestViewModel();

        try
        {
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var categoriesCount = await dbContext.Categories.CountAsync();
            var productsCount = await dbContext.Products.CountAsync();

            model.Success = true;
            model.CategoriesCount = categoriesCount;
            model.ProductsCount = productsCount;
            model.QueryExecutionTime = DateTime.Now;

            logger.LogInformation("Database query test successful - Categories: {CategoriesCount}, Products: {ProductsCount}",
                categoriesCount, productsCount);
        }
        catch (Exception ex)
        {
            model.Success = false;
            model.Error = ex.Message;
            logger.LogError(ex, "Database query test failed");
        }

        return View(model);
    }

    public async Task<IActionResult> CredentialInfo()
    {
        var model = new CredentialInfoViewModel();

        try
        {
            if (_vaultSettings.UseStaticCredentials)
            {
                var staticInfo = await vaultService.GetStaticCredentialInfoAsync();
                if (staticInfo != null)
                {
                    model.Username = staticInfo.Username;
                    model.Password = staticInfo.Password;
                    model.IsVaultGenerated = true; // Static credentials are always managed by Vault
                    model.Success = true;
                    model.RetrievedAt = staticInfo.RetrievedAt;
                    model.IsStatic = true;
                    model.LastRotated = staticInfo.LastRotated;
                    model.NextRotation = staticInfo.NextRotation;
                    model.RotationPeriod = staticInfo.RotationPeriod;

                    logger.LogInformation("Retrieved static credential info for username: {Username}", model.Username);
                }
                else
                {
                    model.Success = false;
                    model.Error = "No static credential info found";
                }
            }
            else
            {
                var currentLease = vaultService.GetCurrentLeaseInfo();
                if (currentLease != null)
                {
                    model.Username = currentLease.Username;
                    model.Password = currentLease.Password;
                    model.IsVaultGenerated = currentLease.Username.StartsWith("v-token-", StringComparison.OrdinalIgnoreCase);
                    model.Success = true;
                    model.RetrievedAt = currentLease.CreatedAt;
                    model.LeaseId = currentLease.LeaseId;
                    model.ExpiresAt = currentLease.ExpiresAt;
                    model.TimeRemaining = currentLease.TimeRemaining;
                    model.IsStatic = false;

                    logger.LogInformation("Retrieved dynamic credential info for username: {Username}", model.Username);
                }
                else
                {
                    model.Success = false;
                    model.Error = "No active lease found";
                }
            }
        }
        catch (Exception ex)
        {
            model.Success = false;
            model.Error = ex.Message;
            logger.LogError(ex, "Failed to retrieve credential info");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> HealthCheck()
    {
        var health = new
        {
            timestamp = DateTime.UtcNow,
            credentialType = _vaultSettings.UseStaticCredentials ? "static" : "dynamic",
            vault = new { healthy = false, error = (string?)null, details = (object?)null },
            database = new { healthy = false, error = (string?)null }
        };

        try
        {
            if (_vaultSettings.UseStaticCredentials)
            {
                var staticInfo = await vaultService.GetStaticCredentialInfoAsync();
                if (staticInfo != null)
                {
                    health = health with
                    {
                        vault = health.vault with
                        {
                            healthy = true,
                            details = new
                            {
                                username = staticInfo.Username,
                                lastRotated = staticInfo.LastRotated,
                                nextRotation = staticInfo.NextRotation,
                                isExpired = staticInfo.IsExpired,
                                timeUntilRotation = staticInfo.TimeUntilRotation
                            }
                        }
                    };
                }
                else
                {
                    health = health with { vault = health.vault with { error = "No static credential info available" } };
                }
            }
            else
            {
                var currentLease = vaultService.GetCurrentLeaseInfo();
                if (currentLease != null && !currentLease.IsExpired)
                {
                    health = health with
                    {
                        vault = health.vault with
                        {
                            healthy = true,
                            details = new
                            {
                                leaseId = currentLease.LeaseId,
                                username = currentLease.Username,
                                expiresAt = currentLease.ExpiresAt,
                                timeRemaining = currentLease.TimeRemaining
                            }
                        }
                    };
                }
                else
                {
                    health = health with { vault = health.vault with { error = "No active lease or lease expired" } };
                }
            }
        }
        catch (Exception ex)
        {
            health = health with { vault = health.vault with { error = ex.Message } };
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.CanConnectAsync();
            health = health with { database = health.database with { healthy = true } };
        }
        catch (Exception ex)
        {
            health = health with { database = health.database with { error = ex.Message } };
        }

        return Json(health);
    }
}
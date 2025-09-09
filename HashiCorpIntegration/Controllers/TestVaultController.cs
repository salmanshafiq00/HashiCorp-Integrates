using Microsoft.AspNetCore.Mvc;
using HashiCorpIntegration.Data;
using HashiCorpIntegration.Vault;
using Microsoft.EntityFrameworkCore;
using HashiCorpIntegration.Models;

namespace HashiCorpIntegration.Controllers;

public class TestVaultController(
    IVaultService vaultService,
    IServiceProvider serviceProvider,
    ILogger<TestVaultController> logger) : Controller
{
    public async Task<IActionResult> Index()
    {
        var model = new VaultDashboardViewModel();

        // Get current lease info (don't create new one)
        var currentLease = vaultService.GetCurrentLeaseInfo();
        if (currentLease != null)
        {
            model.CurrentLease = new CurrentLeaseViewModel
            {
                LeaseId = currentLease.LeaseId,
                Username = currentLease.Username,
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

        // Test Database connection using current lease
        if (currentLease != null && !currentLease.IsExpired)
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

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> GetNewConnection()
    {
        try
        {
            await vaultService.GetNewSqlConnectionStringAsync();
            TempData["Success"] = "New connection credentials obtained successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to get new connection: {ex.Message}";
            logger.LogError(ex, "Failed to get new connection");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RevokeLease(string leaseId)
    {
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

    // Keep existing methods for compatibility
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
        var currentLease = vaultService.GetCurrentLeaseInfo();

        if (currentLease != null)
        {
            model.Username = currentLease.Username;
            model.IsVaultGenerated = currentLease.Username.StartsWith("v-token-", StringComparison.OrdinalIgnoreCase);
            model.Success = true;
            model.RetrievedAt = DateTime.Now;
            model.LeaseId = currentLease.LeaseId;
            model.ExpiresAt = currentLease.ExpiresAt;
            model.TimeRemaining = currentLease.TimeRemaining;

            logger.LogInformation("Retrieved credential info for username: {Username}", model.Username);
        }
        else
        {
            model.Success = false;
            model.Error = "No active lease found";
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> HealthCheck()
    {
        var currentLease = vaultService.GetCurrentLeaseInfo();

        var health = new
        {
            timestamp = DateTime.UtcNow,
            vault = new { healthy = false, error = (string?)null, currentLease = currentLease?.LeaseId },
            database = new { healthy = false, error = (string?)null }
        };

        // Check if we have a current lease (don't create new one)
        if (currentLease != null && !currentLease.IsExpired)
        {
            health = health with { vault = health.vault with { healthy = true } };
        }
        else
        {
            health = health with { vault = health.vault with { error = "No active lease or lease expired" } };
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

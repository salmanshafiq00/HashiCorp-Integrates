using Microsoft.AspNetCore.Mvc;
using HashiCorpIntegration.Data;
using HashiCorpIntegration.Vault;
using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Controllers;

public class TestVaultController : Controller
{
    private readonly IVaultService _vaultService;
    private readonly IApplicationDbContextFactory _dbContextFactory;
    private readonly ILogger<TestVaultController> _logger;

    public TestVaultController(
        IVaultService vaultService,
        IApplicationDbContextFactory dbContextFactory,
        ILogger<TestVaultController> logger)
    {
        _vaultService = vaultService;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var model = new VaultTestResultViewModel();

        // Test Vault connection
        try
        {
            model.VaultConnectionString = await _vaultService.GetSqlConnectionStringAsync();
            model.VaultConnectionSuccess = !string.IsNullOrEmpty(model.VaultConnectionString);
            _logger.LogInformation("Successfully retrieved connection string from Vault");
        }
        catch (Exception ex)
        {
            model.VaultConnectionSuccess = false;
            model.VaultError = ex.Message;
            _logger.LogError(ex, "Vault connection test failed");
        }

        // Test Database connection using factory
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            model.DatabaseConnectionSuccess = await dbContext.Database.CanConnectAsync();
            _logger.LogInformation("Successfully connected to database using dynamic connection");
        }
        catch (Exception ex)
        {
            model.DatabaseConnectionSuccess = false;
            model.DatabaseError = ex.Message;
            _logger.LogError(ex, "Database connection test failed");
        }

        return View(model);
    }

    public async Task<IActionResult> TestQuery()
    {
        var model = new DatabaseQueryTestViewModel();

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Test a simple query
            var categoriesCount = await dbContext.Categories.CountAsync();
            var productsCount = await dbContext.Products.CountAsync();

            model.Success = true;
            model.CategoriesCount = categoriesCount;
            model.ProductsCount = productsCount;
            model.QueryExecutionTime = DateTime.Now;

            _logger.LogInformation("Database query test successful - Categories: {CategoriesCount}, Products: {ProductsCount}",
                categoriesCount, productsCount);
        }
        catch (Exception ex)
        {
            model.Success = false;
            model.Error = ex.Message;
            _logger.LogError(ex, "Database query test failed");
        }

        return View(model);
    }

    public async Task<IActionResult> CredentialInfo()
    {
        var model = new CredentialInfoViewModel();

        try
        {
            var connectionString = await _vaultService.GetSqlConnectionStringAsync();

            // Parse connection string to extract username (safely)
            var parts = connectionString.Split(';');
            var userIdPart = parts.FirstOrDefault(p => p.StartsWith("User Id=", StringComparison.OrdinalIgnoreCase));

            if (userIdPart != null)
            {
                model.Username = userIdPart.Split('=')[1];
                model.IsVaultGenerated = model.Username.StartsWith("v-token-", StringComparison.OrdinalIgnoreCase);
                model.Success = true;
            }

            model.RetrievedAt = DateTime.Now;
            _logger.LogInformation("Retrieved credential info for username: {Username}", model.Username);
        }
        catch (Exception ex)
        {
            model.Success = false;
            model.Error = ex.Message;
            _logger.LogError(ex, "Failed to retrieve credential info");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> HealthCheck()
    {
        var health = new
        {
            timestamp = DateTime.UtcNow,
            vault = new { healthy = false, error = (string?)null },
            database = new { healthy = false, error = (string?)null }
        };

        try
        {
            await _vaultService.GetSqlConnectionStringAsync();
            health = health with { vault = health.vault with { healthy = true } };
        }
        catch (Exception ex)
        {
            health = health with { vault = health.vault with { error = ex.Message } };
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
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

// ViewModels remain the same
public class VaultTestResultViewModel
{
    public bool VaultConnectionSuccess { get; set; }
    public string? VaultConnectionString { get; set; }
    public string? VaultError { get; set; }
    public bool DatabaseConnectionSuccess { get; set; }
    public string? DatabaseError { get; set; }
}

public class DatabaseQueryTestViewModel
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCount { get; set; }
    public DateTime QueryExecutionTime { get; set; }
}

public class CredentialInfoViewModel
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Username { get; set; }
    public bool IsVaultGenerated { get; set; }
    public DateTime RetrievedAt { get; set; }
}
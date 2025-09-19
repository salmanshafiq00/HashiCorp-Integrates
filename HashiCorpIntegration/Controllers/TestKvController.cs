using Microsoft.AspNetCore.Mvc;
using HashiCorpIntegration.Vault;
using Microsoft.Extensions.Options;
using HashiCorpIntegration.src.Models;
using System.Diagnostics;

namespace HashiCorpIntegration.src.Controllers;

public class TestKvController(
    IVaultService vaultService,
    IOptions<VaultSettings> vaultSettings,
    ILogger<TestKvController> logger) : Controller
{
    private readonly VaultSettings _vaultSettings = vaultSettings.Value;

    public async Task<IActionResult> Index(string path = "")
    {
        var model = new KvDashboardViewModel
        {
            CurrentPath = path
        };

        await LoadSecretPaths(model, path);
        await LoadSecrets(model, path);
        await TestVaultConnection(model);

        return View(model);
    }

    private async Task LoadSecretPaths(KvDashboardViewModel model, string basePath)
    {
        try
        {
            model.SecretPaths = await vaultService.ListSecretPathsAsync(basePath);
        }
        catch (Exception ex)
        {
            model.Error = $"Failed to load secret paths: {ex.Message}";
            logger.LogError(ex, "Failed to load secret paths from: {BasePath}", basePath);
        }
    }

    private async Task LoadSecrets(KvDashboardViewModel model, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            var commonPaths = new[] {
                "hashicorp-integration/config",
                "hashicorp-integration/environments/dev",
                "hashicorp-integration/environments/prod"
            };

            foreach (var commonPath in commonPaths)
            {
                try
                {
                    var secrets = await vaultService.GetAllSecretsAsync(commonPath);
                    model.Secrets.Add(new KvSecretViewModel
                    {
                        Path = commonPath,
                        Data = secrets,
                        RetrievedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load secrets from common path: {Path}", commonPath);
                    // Continue with other paths
                }
            }
        }
        else
        {
            try
            {
                var secrets = await vaultService.GetAllSecretsAsync(path);
                model.Secrets.Add(new KvSecretViewModel
                {
                    Path = path,
                    Data = secrets,
                    RetrievedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                model.Error = $"Failed to load secrets from path '{path}': {ex.Message}";
                logger.LogError(ex, "Failed to load secrets from path: {Path}", path);
            }
        }
    }

    private async Task TestVaultConnection(KvDashboardViewModel model)
    {
        try
        {
            // Updated to use the correct path from setup script
            var testSecret = await vaultService.GetSecretAsync("hashicorp-integration/config", "api_key");
            model.VaultConnectionSuccess = !string.IsNullOrEmpty(testSecret);
        }
        catch (Exception ex)
        {
            model.VaultConnectionSuccess = false;
            model.VaultError = ex.Message;
            logger.LogError(ex, "Vault connection test failed");
        }
    }

    [HttpGet]
    public async Task<IActionResult> SecretDetail(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        var model = new KvSecretDetailViewModel { Path = path };

        try
        {
            model.Data = await vaultService.GetAllSecretsAsync(path);
            model.Success = true;
            model.RetrievedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            model.Success = false;
            model.Error = ex.Message;
            logger.LogError(ex, "Failed to load secret detail for path: {Path}", path);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult CreateSecret(string path = "")
    {
        var model = new KvCreateUpdateViewModel
        {
            Path = path,
            IsUpdate = false
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSecret(KvCreateUpdateViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Path))
        {
            ModelState.AddModelError("Path", "Path is required");
            return View(model);
        }

        try
        {
            var secrets = new Dictionary<string, object>();

            foreach (var kvp in model.KeyValuePairs.Where(k => !k.IsEmpty))
            {
                secrets[kvp.Key] = kvp.Value;
            }

            if (!secrets.Any())
            {
                ModelState.AddModelError("", "At least one key-value pair is required");
                return View(model);
            }

            var success = await vaultService.CreateOrUpdateSecretAsync(model.Path, secrets);

            if (success)
            {
                TempData["Success"] = $"Secret created successfully at path: {model.Path}";
                return RedirectToAction(nameof(SecretDetail), new { path = model.Path });
            }
            else
            {
                model.Error = "Failed to create secret";
                return View(model);
            }
        }
        catch (Exception ex)
        {
            model.Error = ex.Message;
            logger.LogError(ex, "Failed to create secret at path: {Path}", model.Path);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> UpdateSecret(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        var model = new KvCreateUpdateViewModel
        {
            Path = path,
            IsUpdate = true
        };

        try
        {
            var existingSecrets = await vaultService.GetAllSecretsAsync(path);
            model.KeyValuePairs = existingSecrets.Select(kvp => new KvKeyValuePair
            {
                Key = kvp.Key,
                Value = kvp.Value?.ToString() ?? ""
            }).ToList();

            // Add one empty row for new keys
            model.KeyValuePairs.Add(new KvKeyValuePair());
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to load existing secret: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        return View("CreateSecret", model);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSecret(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var success = await vaultService.DeleteSecretAsync(path);

            if (success)
            {
                TempData["Success"] = $"Secret deleted successfully: {path}";
            }
            else
            {
                TempData["Error"] = $"Failed to delete secret: {path}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to delete secret: {ex.Message}";
            logger.LogError(ex, "Failed to delete secret at path: {Path}", path);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSecretKey(string path, string key)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(key))
        {
            TempData["Error"] = "Path and key are required";
            return RedirectToAction(nameof(SecretDetail), new { path });
        }

        try
        {
            var success = await vaultService.DeleteSecretKeyAsync(path, key);

            if (success)
            {
                TempData["Success"] = $"Secret key '{key}' deleted successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to delete secret key: {key}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to delete secret key: {ex.Message}";
            logger.LogError(ex, "Failed to delete secret key {Key} at path: {Path}", key, path);
        }

        return RedirectToAction(nameof(SecretDetail), new { path });
    }

    [HttpPost]
    public async Task<IActionResult> AddSecretKey(string path, string key, string value)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(key))
        {
            TempData["Error"] = "Path and key are required";
            return RedirectToAction(nameof(SecretDetail), new { path });
        }

        try
        {
            var success = await vaultService.CreateOrUpdateSecretKeyAsync(path, key, value ?? "");

            if (success)
            {
                TempData["Success"] = $"Secret key '{key}' added/updated successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to add/update secret key: {key}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to add/update secret key: {ex.Message}";
            logger.LogError(ex, "Failed to add/update secret key {Key} at path: {Path}", key, path);
        }

        return RedirectToAction(nameof(SecretDetail), new { path });
    }

    [HttpPost]
    public IActionResult InvalidateCache(string path = "")
    {
        try
        {
            vaultService.InvalidateKvCache(string.IsNullOrEmpty(path) ? null : path);

            if (string.IsNullOrEmpty(path))
            {
                TempData["Success"] = "All KV cache invalidated successfully";
            }
            else
            {
                TempData["Success"] = $"Cache invalidated for path: {path}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to invalidate cache: {ex.Message}";
            logger.LogError(ex, "Failed to invalidate KV cache for path: {Path}", path);
        }

        return RedirectToAction(nameof(Index), new { path });
    }

    public async Task<IActionResult> TestKv()
    {
        var model = new KvTestViewModel
        {
            TestPath = "hashicorp-integration/config", 
            TestKey = "api_key"
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Updated to use the correct path from setup script
            var value = await vaultService.GetSecretAsync(model.TestPath, model.TestKey);
            stopwatch.Stop();

            model.Success = true;
            model.RetrievedValue = value;
            model.TestExecutedAt = DateTime.UtcNow;
            model.ResponseTime = stopwatch.Elapsed;

            logger.LogInformation("KV test successful - Retrieved secret from {Path}/{Key}",
                model.TestPath, model.TestKey);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            model.Success = false;
            model.Error = ex.Message;
            model.TestExecutedAt = DateTime.UtcNow;
            model.ResponseTime = stopwatch.Elapsed;
            logger.LogError(ex, "KV test failed for {Path}/{Key}", model.TestPath, model.TestKey);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> HealthCheck()
    {
        var health = new
        {
            timestamp = DateTime.UtcNow,
            service = "KV Management",
            vault = new { healthy = false, error = (string?)null, details = (object?)null }
        };

        try
        {
            // Updated to use the correct path from setup script
            var testSecrets = await vaultService.GetAllSecretsAsync("hashicorp-integration/config");

            health = health with
            {
                vault = health.vault with
                {
                    healthy = true,
                    details = new
                    {
                        secretCount = testSecrets.Count,
                        testPath = "hashicorp-integration/config",
                        cacheStatus = "active"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            health = health with { vault = health.vault with { error = ex.Message } };
        }

        return Json(health);
    }

    // Additional KV management endpoints

    [HttpGet]
    public async Task<IActionResult> SecretHistory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var metadata = await vaultService.GetSecretMetadataAsync(path);
            ViewBag.Path = path;
            ViewBag.Metadata = metadata;
            return View(metadata);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to load secret history: {ex.Message}";
            return RedirectToAction(nameof(SecretDetail), new { path });
        }
    }

    [HttpGet]
    public async Task<IActionResult> SecretVersion(string path, int version)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var secrets = await vaultService.GetSecretVersionAsync(path, version);
            var model = new KvSecretDetailViewModel
            {
                Path = path,
                Data = secrets,
                Success = true,
                RetrievedAt = DateTime.UtcNow
            };
            ViewBag.Version = version;
            return View("SecretDetail", model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to load secret version {version}: {ex.Message}";
            return RedirectToAction(nameof(SecretDetail), new { path });
        }
    }

    [HttpPost]
    public async Task<IActionResult> UndeleteSecret(string path, int version)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var success = await vaultService.UndeleteSecretAsync(path, version);
            if (success)
            {
                TempData["Success"] = $"Secret version {version} undeleted successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to undelete secret version {version}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to undelete secret version {version}: {ex.Message}";
            logger.LogError(ex, "Failed to undelete secret version {Version} at path: {Path}", version, path);
        }

        return RedirectToAction(nameof(SecretDetail), new { path });
    }

    [HttpPost]
    public async Task<IActionResult> DestroySecret(string path, int version)
    {
        if (string.IsNullOrEmpty(path))
        {
            TempData["Error"] = "Secret path is required";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var success = await vaultService.DestroySecretAsync(path, version);
            if (success)
            {
                TempData["Success"] = $"Secret version {version} destroyed permanently";
            }
            else
            {
                TempData["Error"] = $"Failed to destroy secret version {version}";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to destroy secret version {version}: {ex.Message}";
            logger.LogError(ex, "Failed to destroy secret version {Version} at path: {Path}", version, path);
        }

        return RedirectToAction(nameof(SecretDetail), new { path });
    }

    [HttpGet]
    public async Task<IActionResult> SecretExists(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return Json(new { exists = false, error = "Path is required" });
        }

        try
        {
            var exists = await vaultService.SecretExistsAsync(path);
            return Json(new { exists, path });
        }
        catch (Exception ex)
        {
            return Json(new { exists = false, error = ex.Message, path });
        }
    }
}
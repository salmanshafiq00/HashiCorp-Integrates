using HashiCorpIntegration.Data;
using HashiCorpIntegration.Jobs;
using HashiCorpIntegration.Vault;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Vault settings
builder.Services.Configure<VaultSettings>(builder.Configuration.GetSection(VaultSettings.SectionName));

// Add services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IVaultService, VaultService>();

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var vaultService = serviceProvider.GetRequiredService<IVaultService>();
    var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
    var connectionString = GetConnectionStringWithRetry(vaultService, logger);
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        // Add connection resiliency
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: [18456]); // Login failed error

        // Set command timeout for long operations
        sqlOptions.CommandTimeout(300); // 5 minutes for bulk operations
    });

    // Enable sensitive data logging in development
    if (serviceProvider.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
    {
        options.EnableSensitiveDataLogging();
    }
});

// Register the interface
builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Add the background service
builder.Services.AddHostedService<VaultCredentialRefreshService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


static string GetConnectionStringWithRetry(IVaultService vaultService, ILogger logger)
{
    try
    {
        return vaultService.GetSqlConnectionStringAsync().GetAwaiter().GetResult();
    }
    catch (SqlException ex) when (ex.Number == 18456) // Login failed
    {
        logger.LogWarning("Database login failed, invalidating cache and retrying");
        vaultService.InvalidateConnectionCache();

        try
        {
            return vaultService.GetSqlConnectionStringAsync().GetAwaiter().GetResult();
        }
        catch
        {
            logger.LogError("Failed to get new credentials, falling back to static connection");
             throw;
        }
    }
    catch
    {
        logger.LogError("Failed to get vault credentials, using fallback connection");
        throw;
    }
}
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

// Register DbContext with factory function that resolves connection string at runtime
//builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
//{
//    // This will be called each time a DbContext is needed
//    var vaultService = serviceProvider.GetRequiredService<IVaultService>();
//    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

//    string connectionString;
//    try
//    {
//        connectionString = vaultService.GetSqlConnectionStringAsync().GetAwaiter().GetResult();
//    }
//    catch
//    {
//        // Fallback to static connection string
//        connectionString = configuration.GetConnectionString("DefaultConnection")!;
//    }

//    options.UseSqlServer(connectionString);
//});

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var vaultService = serviceProvider.GetRequiredService<IVaultService>();
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

    // Create a connection string provider that can refresh on failures
    options.UseSqlServer(connectionString => GetConnectionStringWithRetry(vaultService, configuration, logger));
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


static string GetConnectionStringWithRetry(IVaultService vaultService, IConfiguration configuration, ILogger logger)
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
            return configuration.GetConnectionString("DefaultConnection")!;
        }
    }
    catch
    {
        logger.LogError("Failed to get vault credentials, using fallback connection");
        return configuration.GetConnectionString("DefaultConnection")!;
    }
}
using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Data;

public class ApplicationDbContextFactory : IApplicationDbContextFactory
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApplicationDbContextFactory> _logger;

    public ApplicationDbContextFactory(
        IConnectionStringProvider connectionStringProvider,
        IConfiguration configuration,
        ILogger<ApplicationDbContextFactory> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        try
        {
            var connectionString = await _connectionStringProvider.GetConnectionStringAsync();

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            _logger.LogDebug("Created DbContext with dynamic connection string");
            return new ApplicationDbContext(optionsBuilder.Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DbContext with dynamic connection, using fallback");

            // Fallback to static connection
            var fallbackConnectionString = _configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(fallbackConnectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
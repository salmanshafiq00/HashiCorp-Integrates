using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Data;

public class DynamicDbContextProvider : IDynamicDbContextProvider, IDisposable
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DynamicDbContextProvider> _logger;
    private ApplicationDbContext? _currentContext;
    private string? _currentConnectionString;

    public DynamicDbContextProvider(
        IConnectionStringProvider connectionStringProvider,
        IConfiguration configuration,
        ILogger<DynamicDbContextProvider> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public IApplicationDbContext GetContext()
    {
        try
        {
            var connectionString = _connectionStringProvider.GetConnectionStringAsync().GetAwaiter().GetResult();

            // Only create new context if connection string changed or context doesn't exist
            if (_currentContext == null || _currentConnectionString != connectionString)
            {
                _currentContext?.Dispose();

                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connectionString);

                _currentContext = new ApplicationDbContext(optionsBuilder.Options);
                _currentConnectionString = connectionString;

                _logger.LogDebug("Created new DbContext with updated connection string");
            }

            return _currentContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DbContext, using fallback");

            // Fallback to static connection
            var fallbackConnectionString = _configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(fallbackConnectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }

    public void Dispose()
    {
        _currentContext?.Dispose();
    }
}
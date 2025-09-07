using HashiCorpIntegration.Data;
using HashiCorpIntegration.Jobs;
using HashiCorpIntegration.Vault;

var builder = WebApplication.CreateBuilder(args);

// Configure Vault settings
builder.Services.Configure<VaultSettings>(builder.Configuration.GetSection(VaultSettings.SectionName));

// Add services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IVaultService, VaultService>();
builder.Services.AddScoped<IConnectionStringProvider, VaultConnectionStringProvider>();

// Configure DbContext with connection string provider
//builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
//{
//    // Use fallback connection string for initial setup
//    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
//    var fallbackConnectionString = configuration.GetConnectionString("DefaultConnection");
//    options.UseSqlServer(fallbackConnectionString);
//});

//builder.Services.AddDbContext<ApplicationDbContext>(async (serviceProvider, options) =>
//{
//    var connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();
//    var connectionString = await connectionStringProvider.GetConnectionStringAsync();
//    options.UseSqlServer(connectionString);
//});

// Register the DbContext Factory
builder.Services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactory>();

// Remove the commented DbContext registrations and factory registration
// Add these instead:

builder.Services.AddScoped<IDynamicDbContextProvider, DynamicDbContextProvider>();
builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<IDynamicDbContextProvider>().GetContext());

// Add the background service
builder.Services.AddHostedService<VaultCredentialRefreshService>();

// Then use a background service to update connection string if needed

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{

    //using (var scope = app.Services.CreateScope())
    //{
    //    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    //    dbContext.Database.Migrate();
    //}
}

    app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


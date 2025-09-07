using HashiCorpIntegration.Entities;
using Microsoft.EntityFrameworkCore;

namespace HashiCorpIntegration.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    public DbSet<Product> Products { get; set; } = default!;
    public DbSet<Category> Categories { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.HasData(
                new Category { Id = 1, Name = "Electronics" },
                new Category { Id = 2, Name = "Books" },
                new Category { Id = 3, Name = "Clothing" },
                new Category { Id = 4, Name = "Home & Kitchen" },
                new Category { Id = 5, Name = "Sports & Outdoors" }
            );
        });

        // Product configuration
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name)
                  .IsRequired()
                  .HasMaxLength(200);
            entity.Property(p => p.Price)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();
            entity.Property(p => p.Description)
                  .HasMaxLength(1000)
                  .IsRequired(false);
            entity.Property(p => p.ImageUrl)
                  .HasMaxLength(500)
                  .IsRequired(false);
            entity.Property(p => p.CreatedDate)
                  .IsRequired();
            entity.Property(p => p.StockQuantity)
                  .IsRequired();

            entity.HasOne(p => p.Category)
                  .WithMany()
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new Product
                {
                    Id = 1,
                    Name = "Smartphone",
                    Price = 699.99m,
                    Description = "Latest model smartphone with advanced features.",
                    CategoryId = 1,
                    StockQuantity = 50,
                    ImageUrl = "https://example.com/images/smartphone.jpg",
                    CreatedDate = DateTime.UtcNow
                },
                new Product
                {
                    Id = 2,
                    Name = "Laptop",
                    Price = 999.99m,
                    Description = "High-performance laptop for work and play.",
                    CategoryId = 1,
                    StockQuantity = 30,
                    ImageUrl = "https://example.com/images/laptop.jpg",
                    CreatedDate = DateTime.UtcNow
                },
                new Product
                {
                    Id = 3,
                    Name = "Science Fiction Novel",
                    Price = 19.99m,
                    Description = "A thrilling science fiction novel set in a dystopian future.",
                    CategoryId = 2,
                    StockQuantity = 100,
                    ImageUrl = "https://example.com/images/scifi_novel.jpg",
                    CreatedDate = DateTime.UtcNow
                },
                new Product
                {
                    Id = 4,
                    Name = "T-Shirt",
                    Price = 14.99m,
                    Description = "Comfortable cotton t-shirt available in various sizes.",
                    CategoryId = 3,
                    StockQuantity = 200,
                    ImageUrl = "https://example.com/images/tshirt.jpg",
                    CreatedDate = DateTime.UtcNow
                },
                new Product
                {
                    Id = 5,
                    Name = "Blender",
                    Price = 49.99m,
                    Description = "Powerful kitchen blender for smoothies and more.",
                    CategoryId = 4,
                    StockQuantity = 80,
                    ImageUrl = "https://example.com/images/blender.jpg",
                    CreatedDate = DateTime.UtcNow
                },
                new Product
                {
                    Id = 6,
                    Name = "Yoga Mat",
                    Price = 29.99m,
                    Description = "Non-slip yoga mat for all types of exercise.",
                    CategoryId = 5,
                    StockQuantity = 150,
                    ImageUrl = "https://example.com/images/yoga_mat.jpg",
                    CreatedDate = DateTime.UtcNow
                }
            );
        });
    }
}



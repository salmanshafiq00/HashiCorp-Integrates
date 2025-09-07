using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HashiCorpIntegration.Migrations
{
    /// <inheritdoc />
    public partial class InitializeMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Electronics" },
                    { 2, "Books" },
                    { 3, "Clothing" },
                    { 4, "Home & Kitchen" },
                    { 5, "Sports & Outdoors" }
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CategoryId", "CreatedDate", "Description", "ImageUrl", "Name", "Price", "StockQuantity" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2025, 9, 4, 11, 17, 54, 202, DateTimeKind.Utc).AddTicks(9831), "Latest model smartphone with advanced features.", "https://example.com/images/smartphone.jpg", "Smartphone", 699.99m, 50 },
                    { 2, 1, new DateTime(2025, 9, 4, 11, 17, 54, 202, DateTimeKind.Utc).AddTicks(9834), "High-performance laptop for work and play.", "https://example.com/images/laptop.jpg", "Laptop", 999.99m, 30 },
                    { 3, 2, new DateTime(2025, 9, 4, 11, 17, 54, 202, DateTimeKind.Utc).AddTicks(9835), "A thrilling science fiction novel set in a dystopian future.", "https://example.com/images/scifi_novel.jpg", "Science Fiction Novel", 19.99m, 100 },
                    { 4, 3, new DateTime(2025, 9, 4, 11, 17, 54, 202, DateTimeKind.Utc).AddTicks(9837), "Comfortable cotton t-shirt available in various sizes.", "https://example.com/images/tshirt.jpg", "T-Shirt", 14.99m, 200 },
                    { 5, 4, new DateTime(2025, 9, 4, 11, 17, 54, 202, DateTimeKind.Utc).AddTicks(9838), "Powerful kitchen blender for smoothies and more.", "https://example.com/images/blender.jpg", "Blender", 49.99m, 80 },
                    { 6, 5, new DateTime(2025, 9, 4, 11, 17, 54, 202, DateTimeKind.Utc).AddTicks(9839), "Non-slip yoga mat for all types of exercise.", "https://example.com/images/yoga_mat.jpg", "Yoga Mat", 29.99m, 150 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}

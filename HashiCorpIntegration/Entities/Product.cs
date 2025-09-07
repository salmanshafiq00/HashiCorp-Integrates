using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HashiCorpIntegration.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int StockQuantity { get; set; } = 0;
    public string? ImageUrl { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }

    [ValidateNever]
    public Category Category { get; set; } = default!;
}

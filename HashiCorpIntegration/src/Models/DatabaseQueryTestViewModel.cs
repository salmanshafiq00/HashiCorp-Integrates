namespace HashiCorpIntegration.src.Models;

public class DatabaseQueryTestViewModel
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCount { get; set; }
    public DateTime QueryExecutionTime { get; set; }
}

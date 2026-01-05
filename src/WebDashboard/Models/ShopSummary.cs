namespace WebDashboard.Models;

public class ShopSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}
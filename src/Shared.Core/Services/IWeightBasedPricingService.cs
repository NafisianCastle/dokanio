using Shared.Core.Entities;

namespace Shared.Core.Services;

/// <summary>
/// Interface for weight-based pricing calculations and validation
/// </summary>
public interface IWeightBasedPricingService
{
    /// <summary>
    /// Calculates the price for a weight-based product
    /// </summary>
    /// <param name="product">The weight-based product</param>
    /// <param name="weight">Weight in kilograms</param>
    /// <returns>Calculated price</returns>
    Task<decimal> CalculatePriceAsync(Product product, decimal weight);
    
    /// <summary>
    /// Validates weight input for a product
    /// </summary>
    /// <param name="weight">Weight to validate</param>
    /// <param name="product">Product to validate against</param>
    /// <returns>True if weight is valid</returns>
    Task<bool> ValidateWeightAsync(decimal weight, Product product);
    
    /// <summary>
    /// Gets detailed pricing information for a weight-based product
    /// </summary>
    /// <param name="product">The weight-based product</param>
    /// <param name="weight">Weight in kilograms</param>
    /// <returns>Detailed pricing result</returns>
    Task<WeightPricingResult> GetPricingDetailsAsync(Product product, decimal weight);
    
    /// <summary>
    /// Formats weight for display according to product precision
    /// </summary>
    /// <param name="weight">Weight to format</param>
    /// <param name="precision">Number of decimal places</param>
    /// <returns>Formatted weight string</returns>
    string FormatWeight(decimal weight, int precision);
    
    /// <summary>
    /// Rounds weight to the specified precision
    /// </summary>
    /// <param name="weight">Weight to round</param>
    /// <param name="precision">Number of decimal places</param>
    /// <returns>Rounded weight</returns>
    decimal RoundWeight(decimal weight, int precision);
}

/// <summary>
/// Result of weight-based pricing calculation
/// </summary>
public class WeightPricingResult
{
    public decimal Weight { get; set; }
    public decimal RatePerKilogram { get; set; }
    public decimal TotalPrice { get; set; }
    public string FormattedWeight { get; set; } = string.Empty;
    public string FormattedRate { get; set; } = string.Empty;
    public string FormattedPrice { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}
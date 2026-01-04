using Shared.Core.Entities;
using System.Globalization;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of weight-based pricing service
/// </summary>
public class WeightBasedPricingService : IWeightBasedPricingService
{
    private const decimal MinWeight = 0.001m; // Minimum weight of 1 gram
    private const decimal MaxWeight = 999.999m; // Maximum weight of 999.999 kg
    private const int MaxPrecision = 6;

    public async Task<decimal> CalculatePriceAsync(Product product, decimal weight)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        if (!product.IsWeightBased)
            throw new InvalidOperationException("Product is not weight-based");

        if (!product.RatePerKilogram.HasValue)
            throw new InvalidOperationException("Product does not have a rate per kilogram defined");

        if (!await ValidateWeightAsync(weight, product))
            throw new ArgumentException("Invalid weight value", nameof(weight));

        var roundedWeight = RoundWeight(weight, product.WeightPrecision);
        var totalPrice = roundedWeight * product.RatePerKilogram.Value;
        
        // Round to 2 decimal places for currency
        return Math.Round(totalPrice, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<bool> ValidateWeightAsync(decimal weight, Product product)
    {
        await Task.CompletedTask; // For async consistency

        // Check basic weight constraints
        if (weight <= 0)
            return false;

        if (weight < MinWeight)
            return false;

        if (weight > MaxWeight)
            return false;

        // Check precision constraints
        if (product.WeightPrecision < 0 || product.WeightPrecision > MaxPrecision)
            return false;

        // Check if weight has more decimal places than allowed precision
        var roundedWeight = RoundWeight(weight, product.WeightPrecision);
        return Math.Abs(weight - roundedWeight) < 0.0000001m; // Allow for floating point precision issues
    }

    public async Task<WeightPricingResult> GetPricingDetailsAsync(Product product, decimal weight)
    {
        var result = new WeightPricingResult
        {
            Weight = weight,
            RatePerKilogram = product.RatePerKilogram ?? 0,
            IsValid = true
        };

        try
        {
            // Validate inputs
            if (product == null)
            {
                result.ValidationErrors.Add("Product cannot be null");
                result.IsValid = false;
                return result;
            }

            if (!product.IsWeightBased)
            {
                result.ValidationErrors.Add("Product is not weight-based");
                result.IsValid = false;
                return result;
            }

            if (!product.RatePerKilogram.HasValue)
            {
                result.ValidationErrors.Add("Product does not have a rate per kilogram defined");
                result.IsValid = false;
                return result;
            }

            if (!await ValidateWeightAsync(weight, product))
            {
                result.ValidationErrors.Add($"Weight must be between {MinWeight} and {MaxWeight} kg with maximum {product.WeightPrecision} decimal places");
                result.IsValid = false;
                return result;
            }

            // Calculate pricing
            var roundedWeight = RoundWeight(weight, product.WeightPrecision);
            result.Weight = roundedWeight;
            result.TotalPrice = await CalculatePriceAsync(product, weight);

            // Format for display
            result.FormattedWeight = FormatWeight(roundedWeight, product.WeightPrecision);
            result.FormattedRate = product.RatePerKilogram.Value.ToString("C", CultureInfo.CurrentCulture);
            result.FormattedPrice = result.TotalPrice.ToString("C", CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            result.ValidationErrors.Add(ex.Message);
            result.IsValid = false;
        }

        return result;
    }

    public string FormatWeight(decimal weight, int precision)
    {
        if (precision < 0 || precision > MaxPrecision)
            precision = 3; // Default precision

        var format = precision == 0 ? "F0" : $"F{precision}";
        return $"{weight.ToString(format)} kg";
    }

    public decimal RoundWeight(decimal weight, int precision)
    {
        if (precision < 0 || precision > MaxPrecision)
            precision = 3; // Default precision

        return Math.Round(weight, precision, MidpointRounding.AwayFromZero);
    }
}
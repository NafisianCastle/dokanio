using Shared.Core.DTOs;
using Shared.Core.Entities;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Integrated POS service that coordinates all advanced features
/// </summary>
public interface IIntegratedPosService
{
    /// <summary>
    /// Creates a new sale with full integration support
    /// </summary>
    /// <param name="request">Sale creation request</param>
    /// <returns>Created sale with all integrations applied</returns>
    Task<IntegratedSaleResult> CreateIntegratedSaleAsync(IntegratedSaleRequest request);
    
    /// <summary>
    /// Adds an item to a sale with full discount and pricing integration
    /// </summary>
    /// <param name="request">Add item request</param>
    /// <returns>Updated sale with recalculated totals</returns>
    Task<IntegratedSaleResult> AddItemToIntegratedSaleAsync(AddItemToSaleRequest request);
    
    /// <summary>
    /// Adds a weight-based item to a sale with full integration
    /// </summary>
    /// <param name="request">Add weight-based item request</param>
    /// <returns>Updated sale with recalculated totals</returns>
    Task<IntegratedSaleResult> AddWeightBasedItemToIntegratedSaleAsync(AddWeightBasedItemRequest request);
    
    /// <summary>
    /// Completes a sale with all integrations (discounts, membership, tax, license checks)
    /// </summary>
    /// <param name="request">Complete sale request</param>
    /// <returns>Completed sale with all calculations</returns>
    Task<IntegratedSaleResult> CompleteIntegratedSaleAsync(CompleteSaleRequest request);
    
    /// <summary>
    /// Gets comprehensive sale information with all integrations
    /// </summary>
    /// <param name="saleId">Sale ID</param>
    /// <returns>Complete sale information</returns>
    Task<IntegratedSaleResult> GetIntegratedSaleAsync(Guid saleId);
    
    /// <summary>
    /// Validates system status for sale operations (license, configuration, etc.)
    /// </summary>
    /// <returns>System validation result</returns>
    Task<SystemValidationResult> ValidateSystemForSaleAsync();
    
    /// <summary>
    /// Gets system configuration summary for POS operations
    /// </summary>
    /// <returns>Configuration summary</returns>
    Task<PosConfigurationSummary> GetPosConfigurationAsync();
}
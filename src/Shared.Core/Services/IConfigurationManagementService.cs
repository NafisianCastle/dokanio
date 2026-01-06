using Shared.Core.DTOs;
using Shared.Core.Enums;

namespace Shared.Core.Services;

/// <summary>
/// Interface for high-level configuration management operations
/// </summary>
public interface IConfigurationManagementService
{
    /// <summary>
    /// Applies a configuration profile for a specific business type
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="businessType">Business type</param>
    /// <returns>Task</returns>
    Task ApplyBusinessTypeConfigurationAsync(Guid shopId, BusinessType businessType);
    
    /// <summary>
    /// Exports all configurations for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Configuration export data</returns>
    Task<ConfigurationExport> ExportShopConfigurationAsync(Guid shopId);
    
    /// <summary>
    /// Imports configurations for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <param name="export">Configuration export data</param>
    /// <returns>Task</returns>
    Task ImportShopConfigurationAsync(Guid shopId, ConfigurationExport export);
    
    /// <summary>
    /// Validates all configurations for a shop
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Validation results</returns>
    Task<ConfigurationValidationSummary> ValidateShopConfigurationAsync(Guid shopId);
    
    /// <summary>
    /// Gets recommended configuration settings based on business analytics
    /// </summary>
    /// <param name="shopId">Shop identifier</param>
    /// <returns>Configuration recommendations</returns>
    Task<ConfigurationRecommendations> GetConfigurationRecommendationsAsync(Guid shopId);
}
using Microsoft.Extensions.Logging;
using Shared.Core.Architecture;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shared.Core.Integration;

/// <summary>
/// Implementation of analytics API client
/// </summary>
public class AnalyticsApiClient : IAnalyticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AnalyticsApiConfiguration _configuration;
    private readonly ILogger<AnalyticsApiClient> _logger;

    public AnalyticsApiClient(
        HttpClient httpClient, 
        AnalyticsApiConfiguration configuration,
        ILogger<AnalyticsApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AnalyticsApiResponse> SendSalesDataAsync(SalesAnalyticsData salesData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending sales data for business {BusinessId} with {SalesCount} sales", 
                salesData.BusinessId, salesData.Sales.Count);

            var response = await _httpClient.PostAsJsonAsync("/api/analytics/sales", salesData, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AnalyticsApiResponse>(cancellationToken);
                return result ?? new AnalyticsApiResponse { Success = false, Message = "Empty response" };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Analytics API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AnalyticsApiResponse 
                { 
                    Success = false, 
                    Message = $"API Error: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending sales data to analytics API");
            return new AnalyticsApiResponse 
            { 
                Success = false, 
                Message = ex.Message 
            };
        }
    }

    public async Task<AnalyticsApiResponse> SendInventoryDataAsync(InventoryAnalyticsData inventoryData, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending inventory data for business {BusinessId} with {InventoryCount} items", 
                inventoryData.BusinessId, inventoryData.Inventory.Count);

            var response = await _httpClient.PostAsJsonAsync("/api/analytics/inventory", inventoryData, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AnalyticsApiResponse>(cancellationToken);
                return result ?? new AnalyticsApiResponse { Success = false, Message = "Empty response" };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Analytics API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AnalyticsApiResponse 
                { 
                    Success = false, 
                    Message = $"API Error: {response.StatusCode}" 
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending inventory data to analytics API");
            return new AnalyticsApiResponse 
            { 
                Success = false, 
                Message = ex.Message 
            };
        }
    }

    public async Task<AnalyticsInsightsResponse> GetAnalyticsInsightsAsync(AnalyticsInsightsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting analytics insights for business {BusinessId}", request.BusinessId);

            var response = await _httpClient.PostAsJsonAsync("/api/analytics/insights", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AnalyticsInsightsResponse>(cancellationToken);
                return result ?? new AnalyticsInsightsResponse { Success = false };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Analytics API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AnalyticsInsightsResponse { Success = false };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analytics insights");
            return new AnalyticsInsightsResponse { Success = false };
        }
    }

    public async Task<BusinessPerformanceMetrics> GetBusinessPerformanceAsync(Guid businessId, DateRange period, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting business performance for business {BusinessId}", businessId);

            var queryParams = $"?businessId={businessId}&startDate={period.StartDate:yyyy-MM-dd}&endDate={period.EndDate:yyyy-MM-dd}";
            var response = await _httpClient.GetAsync($"/api/analytics/performance{queryParams}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BusinessPerformanceMetrics>(cancellationToken);
                return result ?? new BusinessPerformanceMetrics { BusinessId = businessId, Period = period };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Analytics API returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new BusinessPerformanceMetrics { BusinessId = businessId, Period = period };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting business performance metrics");
            return new BusinessPerformanceMetrics { BusinessId = businessId, Period = period };
        }
    }
}
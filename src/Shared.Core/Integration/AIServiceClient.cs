using Microsoft.Extensions.Logging;
using Shared.Core.Architecture;
using System.Net.Http.Json;

namespace Shared.Core.Integration;

/// <summary>
/// Implementation of AI service client
/// </summary>
public class AIServiceClient : IAIServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AIServiceConfiguration _configuration;
    private readonly ILogger<AIServiceClient> _logger;

    public AIServiceClient(
        HttpClient httpClient, 
        AIServiceConfiguration configuration,
        ILogger<AIServiceClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AIRecommendationResponse> GetProductRecommendationsAsync(AIRecommendationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting product recommendations for business {BusinessId}, shop {ShopId}", 
                request.BusinessId, request.ShopId);

            var response = await _httpClient.PostAsJsonAsync("/api/ai/recommendations", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AIRecommendationResponse>(cancellationToken);
                return result ?? new AIRecommendationResponse { Success = false };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AIRecommendationResponse { Success = false };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product recommendations from AI service");
            return new AIRecommendationResponse { Success = false };
        }
    }

    public async Task<AIInventoryPredictionResponse> GetInventoryPredictionsAsync(AIInventoryPredictionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting inventory predictions for business {BusinessId}, shop {ShopId}", 
                request.BusinessId, request.ShopId);

            var response = await _httpClient.PostAsJsonAsync("/api/ai/inventory-predictions", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AIInventoryPredictionResponse>(cancellationToken);
                return result ?? new AIInventoryPredictionResponse { Success = false };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AIInventoryPredictionResponse { Success = false };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory predictions from AI service");
            return new AIInventoryPredictionResponse { Success = false };
        }
    }

    public async Task<AIPriceOptimizationResponse> GetPriceOptimizationAsync(AIPriceOptimizationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting price optimization for business {BusinessId}, shop {ShopId}", 
                request.BusinessId, request.ShopId);

            var response = await _httpClient.PostAsJsonAsync("/api/ai/price-optimization", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AIPriceOptimizationResponse>(cancellationToken);
                return result ?? new AIPriceOptimizationResponse { Success = false };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AIPriceOptimizationResponse { Success = false };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price optimization from AI service");
            return new AIPriceOptimizationResponse { Success = false };
        }
    }

    public async Task<AITrainingResponse> TrainModelAsync(AITrainingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Training AI model for business {BusinessId}", request.BusinessId);

            var response = await _httpClient.PostAsJsonAsync("/api/ai/train", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AITrainingResponse>(cancellationToken);
                return result ?? new AITrainingResponse { Success = false };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AITrainingResponse { Success = false };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training AI model");
            return new AITrainingResponse { Success = false };
        }
    }

    public async Task<AIModelStatusResponse> GetModelStatusAsync(Guid businessId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting AI model status for business {BusinessId}", businessId);

            var response = await _httpClient.GetAsync($"/api/ai/model-status?businessId={businessId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AIModelStatusResponse>(cancellationToken);
                return result ?? new AIModelStatusResponse { Success = false };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("AI Service returned error {StatusCode}: {Error}", response.StatusCode, errorContent);
                
                return new AIModelStatusResponse { Success = false };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI model status");
            return new AIModelStatusResponse { Success = false };
        }
    }
}
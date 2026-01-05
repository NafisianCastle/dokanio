using Microsoft.Extensions.Logging;
using Shared.Core.DTOs;
using System.Text.Json;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of data preprocessing service
/// </summary>
public class DataPreprocessingService : IDataPreprocessingService
{
    private readonly ILogger<DataPreprocessingService> _logger;
    private readonly Dictionary<Guid, DataPreprocessingConfig> _configurations = new();

    public DataPreprocessingService(ILogger<DataPreprocessingService> logger)
    {
        _logger = logger;
    }

    public async Task<DataPreprocessingResult> ConfigureAsync(Guid businessId, DataPreprocessingConfig config)
    {
        _logger.LogInformation("Configuring data preprocessing for business {BusinessId}", businessId);

        try
        {
            // Validate configuration
            var validationResult = ValidateConfiguration(config);
            if (!validationResult.IsValid)
            {
                return new DataPreprocessingResult
                {
                    Success = false,
                    Message = $"Invalid configuration: {string.Join(", ", validationResult.Errors)}",
                    BusinessId = businessId
                };
            }

            // Store configuration
            _configurations[businessId] = config;

            var enabledOperations = new List<string>();
            if (config.HandleMissingValues) enabledOperations.Add("MissingValueHandling");
            if (config.RemoveOutliers) enabledOperations.Add("OutlierRemoval");
            if (config.NormalizeFeatures) enabledOperations.Add("FeatureNormalization");

            _logger.LogInformation("Data preprocessing configured for business {BusinessId} with {OperationCount} operations",
                businessId, enabledOperations.Count);

            return new DataPreprocessingResult
            {
                Success = true,
                Message = "Data preprocessing configured successfully",
                BusinessId = businessId,
                Configuration = config,
                EnabledOperations = enabledOperations
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring data preprocessing for business {BusinessId}", businessId);
            return new DataPreprocessingResult
            {
                Success = false,
                Message = $"Error configuring preprocessing: {ex.Message}",
                BusinessId = businessId
            };
        }
    }

    public async Task<PreprocessedSalesData> PreprocessSalesDataAsync(List<SalesDataPoint> salesData, SalesForecastingTrainingConfig config)
    {
        _logger.LogInformation("Preprocessing {RecordCount} sales data points for forecasting", salesData.Count);

        try
        {
            // Convert sales data to dictionary format for processing
            var dataDict = ConvertSalesDataToDictionary(salesData);

            // Apply preprocessing steps
            var processedData = await ApplyPreprocessingPipeline(dataDict);

            // Aggregate data by date for time series forecasting
            var aggregatedData = AggregateDataByDate(processedData);

            // Split data into training, validation, and test sets
            var (trainingSet, validationSet, testSet) = SplitTimeSeriesData(aggregatedData, config.ValidationSplitRatio);

            // Generate quality metrics
            var qualityMetrics = await GenerateQualityMetrics(processedData);

            var result = new PreprocessedSalesData
            {
                TrainingSet = trainingSet,
                ValidationSet = validationSet,
                TestSet = testSet,
                QualityMetrics = qualityMetrics,
                FeatureColumns = new List<string> { "Revenue", "TransactionCount", "ItemCount", "DayOfWeek", "Hour" },
                TargetColumn = "Revenue",
                PreprocessingMetadata = new Dictionary<string, object>
                {
                    ["OriginalRecordCount"] = salesData.Count,
                    ["ProcessedRecordCount"] = processedData.Count,
                    ["AggregatedRecordCount"] = aggregatedData.Count,
                    ["TrainingSetSize"] = trainingSet.Count,
                    ["ValidationSetSize"] = validationSet.Count,
                    ["TestSetSize"] = testSet.Count
                }
            };

            _logger.LogInformation("Sales data preprocessing completed. Training: {TrainingSize}, Validation: {ValidationSize}, Test: {TestSize}",
                trainingSet.Count, validationSet.Count, testSet.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preprocessing sales data");
            return new PreprocessedSalesData
            {
                QualityMetrics = new DataQualityMetrics
                {
                    QualityIssues = new List<string> { $"Preprocessing error: {ex.Message}" }
                }
            };
        }
    }

    public async Task<PreprocessedInteractionData> PreprocessInteractionDataAsync(List<InteractionDataPoint> interactionData, RecommendationTrainingConfig config)
    {
        _logger.LogInformation("Preprocessing {RecordCount} interaction data points for recommendations", interactionData.Count);

        try
        {
            // Filter interactions based on minimum threshold
            var filteredData = FilterInteractionsByMinimumCount(interactionData, config.MinimumInteractions);

            // Create user and item mappings
            var userMapping = CreateUserMapping(filteredData);
            var itemMapping = CreateItemMapping(filteredData);

            // Convert to dictionary format
            var dataDict = ConvertInteractionDataToDictionary(filteredData, userMapping, itemMapping);

            // Apply preprocessing steps
            var processedData = await ApplyPreprocessingPipeline(dataDict);

            // Split data for training and validation
            var (trainingSet, validationSet, testSet) = SplitInteractionData(processedData, 0.8, 0.1);

            // Generate quality metrics
            var qualityMetrics = await GenerateQualityMetrics(processedData);

            var result = new PreprocessedInteractionData
            {
                TrainingSet = trainingSet,
                ValidationSet = validationSet,
                TestSet = testSet,
                UniqueUserCount = userMapping.Count,
                UniqueItemCount = itemMapping.Count,
                UserMapping = userMapping,
                ItemMapping = itemMapping,
                QualityMetrics = qualityMetrics,
                PreprocessingMetadata = new Dictionary<string, object>
                {
                    ["OriginalInteractionCount"] = interactionData.Count,
                    ["FilteredInteractionCount"] = filteredData.Count,
                    ["ProcessedInteractionCount"] = processedData.Count,
                    ["TrainingSetSize"] = trainingSet.Count,
                    ["ValidationSetSize"] = validationSet.Count,
                    ["TestSetSize"] = testSet.Count,
                    ["SparsityRatio"] = CalculateSparsityRatio(filteredData.Count, userMapping.Count, itemMapping.Count)
                }
            };

            _logger.LogInformation("Interaction data preprocessing completed. Users: {UserCount}, Items: {ItemCount}, Interactions: {InteractionCount}",
                userMapping.Count, itemMapping.Count, processedData.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preprocessing interaction data");
            return new PreprocessedInteractionData
            {
                QualityMetrics = new DataQualityMetrics
                {
                    QualityIssues = new List<string> { $"Preprocessing error: {ex.Message}" }
                }
            };
        }
    }

    public async Task<List<Dictionary<string, object>>> HandleMissingValuesAsync(List<Dictionary<string, object>> data, MissingValueStrategy strategy)
    {
        _logger.LogDebug("Handling missing values using strategy: {Strategy}", strategy);

        var processedData = new List<Dictionary<string, object>>();

        switch (strategy)
        {
            case MissingValueStrategy.Mean:
                processedData = HandleMissingValuesWithMean(data);
                break;

            case MissingValueStrategy.Median:
                processedData = HandleMissingValuesWithMedian(data);
                break;

            case MissingValueStrategy.Mode:
                processedData = HandleMissingValuesWithMode(data);
                break;

            case MissingValueStrategy.Forward:
                processedData = HandleMissingValuesWithForwardFill(data);
                break;

            case MissingValueStrategy.Backward:
                processedData = HandleMissingValuesWithBackwardFill(data);
                break;

            case MissingValueStrategy.Remove:
                processedData = RemoveRecordsWithMissingValues(data);
                break;

            default:
                processedData = data.ToList();
                break;
        }

        return processedData;
    }

    public async Task<List<Dictionary<string, object>>> RemoveOutliersAsync(List<Dictionary<string, object>> data, OutlierDetectionMethod method)
    {
        _logger.LogDebug("Removing outliers using method: {Method}", method);

        switch (method)
        {
            case OutlierDetectionMethod.IQR:
                return RemoveOutliersUsingIQR(data);

            case OutlierDetectionMethod.ZScore:
                return RemoveOutliersUsingZScore(data);

            case OutlierDetectionMethod.IsolationForest:
                return RemoveOutliersUsingIsolationForest(data);

            default:
                return data.ToList();
        }
    }

    public async Task<List<Dictionary<string, object>>> NormalizeFeaturesAsync(List<Dictionary<string, object>> data, NormalizationMethod method)
    {
        _logger.LogDebug("Normalizing features using method: {Method}", method);

        switch (method)
        {
            case NormalizationMethod.StandardScaling:
                return ApplyStandardScaling(data);

            case NormalizationMethod.MinMaxScaling:
                return ApplyMinMaxScaling(data);

            case NormalizationMethod.RobustScaling:
                return ApplyRobustScaling(data);

            default:
                return data.ToList();
        }
    }

    public async Task<DataQualityReport> ValidateDataQualityAsync(List<Dictionary<string, object>> data)
    {
        _logger.LogDebug("Validating data quality for {RecordCount} records", data.Count);

        var report = new DataQualityReport
        {
            TotalRecords = data.Count
        };

        if (!data.Any())
        {
            report.OverallQualityScore = 0.0;
            report.Issues.Add(new DataQualityIssue
            {
                Type = DataQualityIssueType.InvalidValues,
                Description = "No data records found",
                Severity = DataQualityIssueSeverity.Critical,
                RecommendedAction = "Ensure data is properly loaded"
            });
            return report;
        }

        // Analyze each column
        var allColumns = data.SelectMany(d => d.Keys).Distinct().ToList();
        
        foreach (var column in allColumns)
        {
            var columnInfo = AnalyzeColumn(data, column);
            report.ColumnInfo[column] = columnInfo;

            // Count missing values
            var missingCount = data.Count(d => !d.ContainsKey(column) || d[column] == null);
            if (missingCount > 0)
            {
                report.MissingValueCounts[column] = missingCount;
                
                if (missingCount > data.Count * 0.5) // More than 50% missing
                {
                    report.Issues.Add(new DataQualityIssue
                    {
                        Type = DataQualityIssueType.MissingValues,
                        Column = column,
                        Description = $"High percentage of missing values ({missingCount}/{data.Count})",
                        AffectedRecords = missingCount,
                        Severity = DataQualityIssueSeverity.High,
                        RecommendedAction = "Consider removing column or using advanced imputation"
                    });
                }
            }
        }

        // Calculate valid records (records with no missing values)
        report.ValidRecords = data.Count(d => allColumns.All(col => d.ContainsKey(col) && d[col] != null));

        // Calculate overall quality score
        report.OverallQualityScore = CalculateOverallQualityScore(report);

        return report;
    }

    #region Private Helper Methods

    private (bool IsValid, List<string> Errors) ValidateConfiguration(DataPreprocessingConfig config)
    {
        var errors = new List<string>();

        // Add validation logic as needed
        if (config.HandleMissingValues && config.MissingValueStrategy == MissingValueStrategy.Remove)
        {
            // This is valid but might result in significant data loss
        }

        return (errors.Count == 0, errors);
    }

    private List<Dictionary<string, object>> ConvertSalesDataToDictionary(List<SalesDataPoint> salesData)
    {
        return salesData.Select(s => new Dictionary<string, object>
        {
            ["Date"] = s.Date,
            ["ShopId"] = s.ShopId,
            ["Revenue"] = s.Revenue,
            ["TransactionCount"] = s.TransactionCount,
            ["ItemCount"] = s.ItemCount,
            ["PaymentMethod"] = s.PaymentMethod,
            ["DayOfWeek"] = (int)s.DayOfWeek,
            ["Hour"] = s.Hour
        }).ToList();
    }

    private async Task<List<Dictionary<string, object>>> ApplyPreprocessingPipeline(List<Dictionary<string, object>> data)
    {
        var processedData = data.ToList();

        // Apply missing value handling
        processedData = await HandleMissingValuesAsync(processedData, MissingValueStrategy.Mean);

        // Apply outlier removal
        processedData = await RemoveOutliersAsync(processedData, OutlierDetectionMethod.IQR);

        // Apply feature normalization
        processedData = await NormalizeFeaturesAsync(processedData, NormalizationMethod.StandardScaling);

        return processedData;
    }

    private List<Dictionary<string, object>> AggregateDataByDate(List<Dictionary<string, object>> data)
    {
        return data
            .GroupBy(d => ((DateTime)d["Date"]).Date)
            .Select(g => new Dictionary<string, object>
            {
                ["Date"] = g.Key,
                ["Revenue"] = g.Sum(d => Convert.ToDecimal(d["Revenue"])),
                ["TransactionCount"] = g.Sum(d => Convert.ToInt32(d["TransactionCount"])),
                ["ItemCount"] = g.Sum(d => Convert.ToInt32(d["ItemCount"])),
                ["DayOfWeek"] = (int)g.Key.DayOfWeek,
                ["IsWeekend"] = g.Key.DayOfWeek == DayOfWeek.Saturday || g.Key.DayOfWeek == DayOfWeek.Sunday
            })
            .OrderBy(d => (DateTime)d["Date"])
            .ToList();
    }

    private (List<Dictionary<string, object>> Training, List<Dictionary<string, object>> Validation, List<Dictionary<string, object>> Test)
        SplitTimeSeriesData(List<Dictionary<string, object>> data, double validationRatio)
    {
        var totalCount = data.Count;
        var validationSize = (int)(totalCount * validationRatio);
        var testSize = (int)(totalCount * 0.1); // 10% for test
        var trainingSize = totalCount - validationSize - testSize;

        var training = data.Take(trainingSize).ToList();
        var validation = data.Skip(trainingSize).Take(validationSize).ToList();
        var test = data.Skip(trainingSize + validationSize).ToList();

        return (training, validation, test);
    }

    private List<InteractionDataPoint> FilterInteractionsByMinimumCount(List<InteractionDataPoint> interactions, int minimumCount)
    {
        var userCounts = interactions.GroupBy(i => i.UserId).ToDictionary(g => g.Key, g => g.Count());
        var itemCounts = interactions.GroupBy(i => i.ItemId).ToDictionary(g => g.Key, g => g.Count());

        return interactions
            .Where(i => userCounts[i.UserId] >= minimumCount && itemCounts[i.ItemId] >= minimumCount)
            .ToList();
    }

    private Dictionary<Guid, int> CreateUserMapping(List<InteractionDataPoint> interactions)
    {
        var uniqueUsers = interactions.Select(i => i.UserId).Distinct().ToList();
        return uniqueUsers.Select((userId, index) => new { userId, index })
                         .ToDictionary(x => x.userId, x => x.index);
    }

    private Dictionary<Guid, int> CreateItemMapping(List<InteractionDataPoint> interactions)
    {
        var uniqueItems = interactions.Select(i => i.ItemId).Distinct().ToList();
        return uniqueItems.Select((itemId, index) => new { itemId, index })
                         .ToDictionary(x => x.itemId, x => x.index);
    }

    private List<Dictionary<string, object>> ConvertInteractionDataToDictionary(
        List<InteractionDataPoint> interactions, 
        Dictionary<Guid, int> userMapping, 
        Dictionary<Guid, int> itemMapping)
    {
        return interactions.Select(i => new Dictionary<string, object>
        {
            ["UserId"] = userMapping[i.UserId],
            ["ItemId"] = itemMapping[i.ItemId],
            ["Rating"] = i.Rating,
            ["Timestamp"] = i.Timestamp,
            ["InteractionType"] = i.InteractionType
        }).ToList();
    }

    private (List<Dictionary<string, object>> Training, List<Dictionary<string, object>> Validation, List<Dictionary<string, object>> Test)
        SplitInteractionData(List<Dictionary<string, object>> data, double trainingRatio, double validationRatio)
    {
        var shuffledData = data.OrderBy(x => Guid.NewGuid()).ToList();
        var totalCount = shuffledData.Count;
        
        var trainingSize = (int)(totalCount * trainingRatio);
        var validationSize = (int)(totalCount * validationRatio);
        
        var training = shuffledData.Take(trainingSize).ToList();
        var validation = shuffledData.Skip(trainingSize).Take(validationSize).ToList();
        var test = shuffledData.Skip(trainingSize + validationSize).ToList();

        return (training, validation, test);
    }

    private double CalculateSparsityRatio(int interactionCount, int userCount, int itemCount)
    {
        var totalPossibleInteractions = (long)userCount * itemCount;
        return totalPossibleInteractions > 0 ? 1.0 - ((double)interactionCount / totalPossibleInteractions) : 0.0;
    }

    private async Task<DataQualityMetrics> GenerateQualityMetrics(List<Dictionary<string, object>> data)
    {
        var report = await ValidateDataQualityAsync(data);
        
        return new DataQualityMetrics
        {
            TotalRecords = report.TotalRecords,
            ValidRecords = report.ValidRecords,
            MissingValueCount = report.MissingValueCounts.Values.Sum(),
            CompletenessScore = report.TotalRecords > 0 ? (double)report.ValidRecords / report.TotalRecords : 0,
            AccuracyScore = report.OverallQualityScore,
            QualityIssues = report.Issues.Select(i => i.Description).ToList()
        };
    }

    private List<Dictionary<string, object>> HandleMissingValuesWithMean(List<Dictionary<string, object>> data)
    {
        if (!data.Any()) return data;

        var numericColumns = GetNumericColumns(data);
        var columnMeans = new Dictionary<string, double>();

        // Calculate means for numeric columns
        foreach (var column in numericColumns)
        {
            var values = data.Where(d => d.ContainsKey(column) && d[column] != null)
                            .Select(d => Convert.ToDouble(d[column]))
                            .ToList();
            
            if (values.Any())
            {
                columnMeans[column] = values.Average();
            }
        }

        // Fill missing values with means
        var processedData = data.Select(record =>
        {
            var newRecord = new Dictionary<string, object>(record);
            foreach (var column in numericColumns)
            {
                if (!newRecord.ContainsKey(column) || newRecord[column] == null)
                {
                    if (columnMeans.ContainsKey(column))
                    {
                        newRecord[column] = columnMeans[column];
                    }
                }
            }
            return newRecord;
        }).ToList();

        return processedData;
    }

    private List<Dictionary<string, object>> HandleMissingValuesWithMedian(List<Dictionary<string, object>> data)
    {
        if (!data.Any()) return data;

        var numericColumns = GetNumericColumns(data);
        var columnMedians = new Dictionary<string, double>();

        foreach (var column in numericColumns)
        {
            var values = data.Where(d => d.ContainsKey(column) && d[column] != null)
                            .Select(d => Convert.ToDouble(d[column]))
                            .OrderBy(v => v)
                            .ToList();
        
            if (values.Any())
            {
                var mid = values.Count / 2;
                columnMedians[column] = (values.Count % 2 != 0)
                    ? values[mid]
                    : (values[mid - 1] + values[mid]) / 2.0;
            }
        }

        var processedData = data.Select(record =>
        {
            var newRecord = new Dictionary<string, object>(record);
            foreach (var column in numericColumns)
            {
                if (!newRecord.ContainsKey(column) || newRecord[column] == null)
                {
                    if (columnMedians.ContainsKey(column))
                    {
                        newRecord[column] = columnMedians[column];
                    }
                }
            }
            return newRecord;
        }).ToList();

        return processedData;
    }

    private List<Dictionary<string, object>> HandleMissingValuesWithMode(List<Dictionary<string, object>> data)
    {
        // Simplified implementation for categorical columns
        return data.ToList(); // Placeholder
    }

    private List<Dictionary<string, object>> HandleMissingValuesWithForwardFill(List<Dictionary<string, object>> data)
    {
        // Forward fill implementation
        return data.ToList(); // Placeholder
    }

    private List<Dictionary<string, object>> HandleMissingValuesWithBackwardFill(List<Dictionary<string, object>> data)
    {
        // Backward fill implementation
        return data.ToList(); // Placeholder
    }

    private List<Dictionary<string, object>> RemoveRecordsWithMissingValues(List<Dictionary<string, object>> data)
    {
        var allColumns = data.SelectMany(d => d.Keys).Distinct().ToList();
        return data.Where(d => allColumns.All(col => d.ContainsKey(col) && d[col] != null)).ToList();
    }

    private List<Dictionary<string, object>> RemoveOutliersUsingIQR(List<Dictionary<string, object>> data)
    {
        if (!data.Any()) return data;

        var numericColumns = GetNumericColumns(data);
        var outlierIndices = new HashSet<int>();

        foreach (var column in numericColumns)
        {
            var values = data.Select((d, index) => new { Value = d.ContainsKey(column) && d[column] != null ? Convert.ToDouble(d[column]) : double.NaN, Index = index })
                            .Where(x => !double.IsNaN(x.Value))
                            .OrderBy(x => x.Value)
                            .ToList();

            if (values.Count < 4) continue; // Need at least 4 values for IQR

            var q1Index = (int)(values.Count * 0.25);
            var q3Index = (int)(values.Count * 0.75);
            var q1 = values[q1Index].Value;
            var q3 = values[q3Index].Value;
            var iqr = q3 - q1;
            var lowerBound = q1 - 1.5 * iqr;
            var upperBound = q3 + 1.5 * iqr;

            foreach (var item in values)
            {
                if (item.Value < lowerBound || item.Value > upperBound)
                {
                    outlierIndices.Add(item.Index);
                }
            }
        }

        return data.Where((d, index) => !outlierIndices.Contains(index)).ToList();
    }

    private List<Dictionary<string, object>> RemoveOutliersUsingZScore(List<Dictionary<string, object>> data)
    {
        // Z-score outlier removal implementation
        return data.ToList(); // Placeholder
    }

    private List<Dictionary<string, object>> RemoveOutliersUsingIsolationForest(List<Dictionary<string, object>> data)
    {
        // Isolation Forest outlier removal implementation
        return data.ToList(); // Placeholder
    }

    private List<Dictionary<string, object>> ApplyStandardScaling(List<Dictionary<string, object>> data)
    {
        if (!data.Any()) return data;

        var numericColumns = GetNumericColumns(data);
        var columnStats = new Dictionary<string, (double Mean, double StdDev)>();

        // Calculate mean and standard deviation for each numeric column
        foreach (var column in numericColumns)
        {
            var values = data.Where(d => d.ContainsKey(column) && d[column] != null)
                            .Select(d => Convert.ToDouble(d[column]))
                            .ToList();

            if (values.Any())
            {
                var mean = values.Average();
                var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
                var stdDev = Math.Sqrt(variance);
                columnStats[column] = (mean, stdDev > 0 ? stdDev : 1.0); // Avoid division by zero
            }
        }

        // Apply standard scaling
        var scaledData = data.Select(record =>
        {
            var newRecord = new Dictionary<string, object>(record);
            foreach (var column in numericColumns)
            {
                if (newRecord.ContainsKey(column) && newRecord[column] != null && columnStats.ContainsKey(column))
                {
                    var value = Convert.ToDouble(newRecord[column]);
                    var (mean, stdDev) = columnStats[column];
                    newRecord[column] = (value - mean) / stdDev;
                }
            }
            return newRecord;
        }).ToList();

        return scaledData;
    }

    private List<Dictionary<string, object>> ApplyMinMaxScaling(List<Dictionary<string, object>> data)
    {
        // Min-Max scaling implementation
        return data.ToList(); // Placeholder
    }

    private List<Dictionary<string, object>> ApplyRobustScaling(List<Dictionary<string, object>> data)
    {
        // Robust scaling implementation
        return data.ToList(); // Placeholder
    }

    private List<string> GetNumericColumns(List<Dictionary<string, object>> data)
    {
        if (!data.Any()) return new List<string>();

        var firstRecord = data.First();
        return firstRecord.Keys.Where(key =>
        {
            var value = firstRecord[key];
            return value != null && (value is int || value is long || value is float || value is double || value is decimal);
        }).ToList();
    }

    private DataTypeInfo AnalyzeColumn(List<Dictionary<string, object>> data, string column)
    {
        var values = data.Where(d => d.ContainsKey(column) && d[column] != null)
                        .Select(d => d[column])
                        .ToList();

        var info = new DataTypeInfo
        {
            NonNullCount = values.Count,
            NullCount = data.Count - values.Count
        };

        if (values.Any())
        {
            var firstValue = values.First();
            info.DataType = firstValue.GetType().Name;

            if (IsNumericType(firstValue))
            {
                var numericValues = values.Select(v => Convert.ToDouble(v)).ToList();
                info.MinValue = numericValues.Min();
                info.MaxValue = numericValues.Max();
                info.Mean = numericValues.Average();
                info.StandardDeviation = Math.Sqrt(numericValues.Sum(v => Math.Pow(v - info.Mean.Value, 2)) / numericValues.Count);
            }

            info.UniqueValues = values.Distinct().Take(10).ToList(); // Limit to first 10 unique values
        }

        return info;
    }

    private bool IsNumericType(object value)
    {
        return value is int || value is long || value is float || value is double || value is decimal;
    }

    private double CalculateOverallQualityScore(DataQualityReport report)
    {
        if (report.TotalRecords == 0) return 0.0;

        var completenessScore = (double)report.ValidRecords / report.TotalRecords;
        var issueScore = Math.Max(0.0, 1.0 - (report.Issues.Count * 0.1)); // Reduce score by 10% per issue

        return (completenessScore + issueScore) / 2.0;
    }

    #endregion
}
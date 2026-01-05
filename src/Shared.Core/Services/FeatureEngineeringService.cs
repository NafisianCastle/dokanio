using Microsoft.Extensions.Logging;

namespace Shared.Core.Services;

/// <summary>
/// Implementation of feature engineering service
/// </summary>
public class FeatureEngineeringService : IFeatureEngineeringService
{
    private readonly ILogger<FeatureEngineeringService> _logger;
    private readonly Dictionary<Guid, FeatureEngineeringConfig> _configurations = new();

    public FeatureEngineeringService(ILogger<FeatureEngineeringService> logger)
    {
        _logger = logger;
    }

    public async Task<FeatureEngineeringResult> ConfigureAsync(Guid businessId, FeatureEngineeringConfig config)
    {
        _logger.LogInformation("Configuring feature engineering for business {BusinessId}", businessId);

        try
        {
            // Store configuration
            _configurations[businessId] = config;

            var enabledFeatureTypes = new List<string>();
            if (config.CreateTimeFeatures) enabledFeatureTypes.Add("TimeFeatures");
            if (config.CreateSeasonalFeatures) enabledFeatureTypes.Add("SeasonalFeatures");
            if (config.CreateLagFeatures) enabledFeatureTypes.Add("LagFeatures");
            if (config.CreateRollingFeatures) enabledFeatureTypes.Add("RollingFeatures");
            if (config.CreateCategoricalFeatures) enabledFeatureTypes.Add("CategoricalFeatures");

            _logger.LogInformation("Feature engineering configured for business {BusinessId} with {FeatureTypeCount} feature types",
                businessId, enabledFeatureTypes.Count);

            return new FeatureEngineeringResult
            {
                Success = true,
                Message = "Feature engineering configured successfully",
                BusinessId = businessId,
                Configuration = config,
                EnabledFeatureTypes = enabledFeatureTypes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring feature engineering for business {BusinessId}", businessId);
            return new FeatureEngineeringResult
            {
                Success = false,
                Message = $"Error configuring feature engineering: {ex.Message}",
                BusinessId = businessId
            };
        }
    }

    public async Task<ForecastingFeatureData> CreateSalesForecastingFeaturesAsync(PreprocessedSalesData preprocessedData, SalesForecastingTrainingConfig config)
    {
        _logger.LogInformation("Creating forecasting features for {RecordCount} records", preprocessedData.TrainingSet.Count);

        try
        {
            var featureData = new ForecastingFeatureData
            {
                TargetColumn = "Revenue"
            };

            // Process each dataset (training, validation, test)
            featureData.TrainingSet = await CreateForecastingFeatures(preprocessedData.TrainingSet, config);
            featureData.ValidationSet = await CreateForecastingFeatures(preprocessedData.ValidationSet, config);
            featureData.TestSet = await CreateForecastingFeatures(preprocessedData.TestSet, config);

            // Extract feature columns (excluding target)
            if (featureData.TrainingSet.Any())
            {
                featureData.FeatureColumns = featureData.TrainingSet.First().Keys
                    .Where(k => k != featureData.TargetColumn)
                    .ToList();
            }

            // Generate feature metadata
            featureData.FeatureMetadata = await GenerateFeatureMetadata(featureData.TrainingSet, featureData.FeatureColumns);

            // Calculate feature importance (simplified)
            featureData.ImportanceScores = await CalculateFeatureImportance(featureData.TrainingSet, featureData.TargetColumn, featureData.FeatureColumns);

            _logger.LogInformation("Created {FeatureCount} forecasting features", featureData.FeatureColumns.Count);

            return featureData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating forecasting features");
            return new ForecastingFeatureData();
        }
    }

    public async Task<RecommendationFeatureData> CreateRecommendationFeaturesAsync(PreprocessedInteractionData preprocessedData, RecommendationTrainingConfig config)
    {
        _logger.LogInformation("Creating recommendation features for {InteractionCount} interactions", preprocessedData.TrainingSet.Count);

        try
        {
            var featureData = new RecommendationFeatureData();

            // Process each dataset
            featureData.TrainingSet = await CreateRecommendationFeatures(preprocessedData.TrainingSet, config);
            featureData.ValidationSet = await CreateRecommendationFeatures(preprocessedData.ValidationSet, config);
            featureData.TestSet = await CreateRecommendationFeatures(preprocessedData.TestSet, config);

            // Define feature categories
            featureData.UserFeatures = new List<string> { "UserId", "UserActivityLevel", "UserPreferenceScore" };
            featureData.ItemFeatures = new List<string> { "ItemId", "ItemPopularity", "ItemCategory" };
            featureData.InteractionFeatures = new List<string> { "Rating", "InteractionType", "TimeOfDay", "DayOfWeek" };

            // Generate feature metadata
            var allFeatures = featureData.UserFeatures.Concat(featureData.ItemFeatures).Concat(featureData.InteractionFeatures).ToList();
            featureData.FeatureMetadata = await GenerateFeatureMetadata(featureData.TrainingSet, allFeatures);

            // Create embeddings (simplified)
            featureData.Embeddings = await CreateUserItemEmbeddings(preprocessedData, config);

            _logger.LogInformation("Created recommendation features: {UserFeatures} user, {ItemFeatures} item, {InteractionFeatures} interaction",
                featureData.UserFeatures.Count, featureData.ItemFeatures.Count, featureData.InteractionFeatures.Count);

            return featureData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating recommendation features");
            return new RecommendationFeatureData();
        }
    }

    public async Task<List<Dictionary<string, object>>> CreateTimeFeaturesAsync(List<Dictionary<string, object>> data, List<string> dateTimeColumns)
    {
        _logger.LogDebug("Creating time features for {ColumnCount} datetime columns", dateTimeColumns.Count);

        var enrichedData = new List<Dictionary<string, object>>();

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            foreach (var column in dateTimeColumns)
            {
                if (record.ContainsKey(column) && record[column] is DateTime dateTime)
                {
                    // Extract time components
                    newRecord[$"{column}_Year"] = dateTime.Year;
                    newRecord[$"{column}_Month"] = dateTime.Month;
                    newRecord[$"{column}_Day"] = dateTime.Day;
                    newRecord[$"{column}_DayOfWeek"] = (int)dateTime.DayOfWeek;
                    newRecord[$"{column}_Hour"] = dateTime.Hour;
                    newRecord[$"{column}_Minute"] = dateTime.Minute;
                    newRecord[$"{column}_Quarter"] = (dateTime.Month - 1) / 3 + 1;
                    newRecord[$"{column}_WeekOfYear"] = GetWeekOfYear(dateTime);
                    newRecord[$"{column}_IsWeekend"] = dateTime.DayOfWeek == DayOfWeek.Saturday || dateTime.DayOfWeek == DayOfWeek.Sunday;
                    newRecord[$"{column}_IsBusinessHour"] = dateTime.Hour >= 9 && dateTime.Hour <= 17;
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    public async Task<List<Dictionary<string, object>>> CreateSeasonalFeaturesAsync(List<Dictionary<string, object>> data, string dateColumn)
    {
        _logger.LogDebug("Creating seasonal features for column {DateColumn}", dateColumn);

        var enrichedData = new List<Dictionary<string, object>>();

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            if (record.ContainsKey(dateColumn) && record[dateColumn] is DateTime dateTime)
            {
                // Seasonal features
                newRecord[$"{dateColumn}_Season"] = GetSeason(dateTime);
                newRecord[$"{dateColumn}_MonthSin"] = Math.Sin(2 * Math.PI * dateTime.Month / 12);
                newRecord[$"{dateColumn}_MonthCos"] = Math.Cos(2 * Math.PI * dateTime.Month / 12);
                newRecord[$"{dateColumn}_DayOfYearSin"] = Math.Sin(2 * Math.PI * dateTime.DayOfYear / 365);
                newRecord[$"{dateColumn}_DayOfYearCos"] = Math.Cos(2 * Math.PI * dateTime.DayOfYear / 365);
                newRecord[$"{dateColumn}_HourSin"] = Math.Sin(2 * Math.PI * dateTime.Hour / 24);
                newRecord[$"{dateColumn}_HourCos"] = Math.Cos(2 * Math.PI * dateTime.Hour / 24);
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    public async Task<List<Dictionary<string, object>>> CreateLagFeaturesAsync(List<Dictionary<string, object>> data, string targetColumn, List<int> lagPeriods)
    {
        _logger.LogDebug("Creating lag features for column {TargetColumn} with {LagCount} lag periods", targetColumn, lagPeriods.Count);

        var sortedData = data.OrderBy(d => d.ContainsKey("Date") ? (DateTime)d["Date"] : DateTime.MinValue).ToList();
        var enrichedData = new List<Dictionary<string, object>>();

        for (int i = 0; i < sortedData.Count; i++)
        {
            var newRecord = new Dictionary<string, object>(sortedData[i]);

            foreach (var lag in lagPeriods)
            {
                var lagIndex = i - lag;
                if (lagIndex >= 0 && sortedData[lagIndex].ContainsKey(targetColumn))
                {
                    newRecord[$"{targetColumn}_Lag{lag}"] = sortedData[lagIndex][targetColumn];
                }
                else
                {
                    newRecord[$"{targetColumn}_Lag{lag}"] = null; // or 0, depending on strategy
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    public async Task<List<Dictionary<string, object>>> CreateRollingFeaturesAsync(List<Dictionary<string, object>> data, string targetColumn, List<int> windowSizes)
    {
        _logger.LogDebug("Creating rolling features for column {TargetColumn} with {WindowCount} window sizes", targetColumn, windowSizes.Count);

        var sortedData = data.OrderBy(d => d.ContainsKey("Date") ? (DateTime)d["Date"] : DateTime.MinValue).ToList();
        var enrichedData = new List<Dictionary<string, object>>();

        for (int i = 0; i < sortedData.Count; i++)
        {
            var newRecord = new Dictionary<string, object>(sortedData[i]);

            foreach (var windowSize in windowSizes)
            {
                var windowStart = Math.Max(0, i - windowSize + 1);
                var windowData = sortedData.Skip(windowStart).Take(i - windowStart + 1)
                    .Where(d => d.ContainsKey(targetColumn) && d[targetColumn] != null)
                    .Select(d => Convert.ToDouble(d[targetColumn]))
                    .ToList();

                if (windowData.Any())
                {
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Mean"] = windowData.Average();
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Sum"] = windowData.Sum();
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Min"] = windowData.Min();
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Max"] = windowData.Max();
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Std"] = CalculateStandardDeviation(windowData);
                }
                else
                {
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Mean"] = null;
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Sum"] = null;
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Min"] = null;
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Max"] = null;
                    newRecord[$"{targetColumn}_Rolling{windowSize}_Std"] = null;
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    public async Task<List<Dictionary<string, object>>> CreateCategoricalFeaturesAsync(List<Dictionary<string, object>> data, List<string> categoricalColumns, CategoricalEncodingMethod encodingMethod)
    {
        _logger.LogDebug("Creating categorical features for {ColumnCount} columns using {Method}", categoricalColumns.Count, encodingMethod);

        switch (encodingMethod)
        {
            case CategoricalEncodingMethod.OneHot:
                return await ApplyOneHotEncoding(data, categoricalColumns);

            case CategoricalEncodingMethod.Label:
                return await ApplyLabelEncoding(data, categoricalColumns);

            case CategoricalEncodingMethod.Frequency:
                return await ApplyFrequencyEncoding(data, categoricalColumns);

            default:
                return data.ToList();
        }
    }

    public async Task<List<Dictionary<string, object>>> CreateInteractionFeaturesAsync(List<Dictionary<string, object>> data, List<(string Column1, string Column2)> columnPairs)
    {
        _logger.LogDebug("Creating interaction features for {PairCount} column pairs", columnPairs.Count);

        var enrichedData = new List<Dictionary<string, object>>();

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            foreach (var (column1, column2) in columnPairs)
            {
                if (record.ContainsKey(column1) && record.ContainsKey(column2) &&
                    record[column1] != null && record[column2] != null)
                {
                    // Numerical interaction
                    if (IsNumeric(record[column1]) && IsNumeric(record[column2]))
                    {
                        var val1 = Convert.ToDouble(record[column1]);
                        var val2 = Convert.ToDouble(record[column2]);
                        
                        newRecord[$"{column1}_{column2}_Multiply"] = val1 * val2;
                        newRecord[$"{column1}_{column2}_Add"] = val1 + val2;
                        newRecord[$"{column1}_{column2}_Ratio"] = val2 != 0 ? val1 / val2 : 0;
                    }
                    // Categorical interaction
                    else
                    {
                        newRecord[$"{column1}_{column2}_Concat"] = $"{record[column1]}_{record[column2]}";
                    }
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    public async Task<FeatureSelectionResult> SelectFeaturesAsync(List<Dictionary<string, object>> data, string targetColumn, FeatureSelectionMethod selectionMethod, int maxFeatures)
    {
        _logger.LogDebug("Selecting features using {Method}, max features: {MaxFeatures}", selectionMethod, maxFeatures);

        try
        {
            var allFeatures = data.First().Keys.Where(k => k != targetColumn).ToList();
            var importanceScores = await CalculateFeatureImportance(data, targetColumn, allFeatures);

            // Select top features based on importance scores
            var selectedFeatures = importanceScores.Scores
                .OrderByDescending(kvp => kvp.Value)
                .Take(Math.Min(maxFeatures, allFeatures.Count))
                .Select(kvp => kvp.Key)
                .ToList();

            // Create dataset with only selected features
            var selectedData = data.Select(record =>
            {
                var newRecord = new Dictionary<string, object> { [targetColumn] = record[targetColumn] };
                foreach (var feature in selectedFeatures)
                {
                    if (record.ContainsKey(feature))
                    {
                        newRecord[feature] = record[feature];
                    }
                }
                return newRecord;
            }).ToList();

            return new FeatureSelectionResult
            {
                SelectedData = selectedData,
                SelectedFeatures = selectedFeatures,
                ImportanceScores = importanceScores,
                OriginalFeatureCount = allFeatures.Count,
                SelectedFeatureCount = selectedFeatures.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting features");
            return new FeatureSelectionResult
            {
                SelectedData = data.ToList(),
                SelectedFeatures = data.First().Keys.Where(k => k != targetColumn).ToList(),
                OriginalFeatureCount = data.First().Keys.Count - 1,
                SelectedFeatureCount = data.First().Keys.Count - 1
            };
        }
    }

    #region Private Helper Methods

    private async Task<List<Dictionary<string, object>>> CreateForecastingFeatures(List<Dictionary<string, object>> data, SalesForecastingTrainingConfig config)
    {
        var enrichedData = data.ToList();

        // Create time features
        if (enrichedData.Any() && enrichedData.First().ContainsKey("Date"))
        {
            enrichedData = await CreateTimeFeaturesAsync(enrichedData, new List<string> { "Date" });
            enrichedData = await CreateSeasonalFeaturesAsync(enrichedData, "Date");
        }

        // Create lag features
        if (config.FeatureColumns.Contains("Revenue"))
        {
            var lagPeriods = new List<int> { 1, 7, 14, 30 }; // 1 day, 1 week, 2 weeks, 1 month
            enrichedData = await CreateLagFeaturesAsync(enrichedData, "Revenue", lagPeriods);
        }

        // Create rolling features
        if (config.FeatureColumns.Contains("Revenue"))
        {
            var windowSizes = new List<int> { 7, 14, 30 }; // 1 week, 2 weeks, 1 month
            enrichedData = await CreateRollingFeaturesAsync(enrichedData, "Revenue", windowSizes);
        }

        return enrichedData;
    }

    private async Task<List<Dictionary<string, object>>> CreateRecommendationFeatures(List<Dictionary<string, object>> data, RecommendationTrainingConfig config)
    {
        var enrichedData = new List<Dictionary<string, object>>();

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            // Add user activity level (simplified)
            if (record.ContainsKey("UserId"))
            {
                newRecord["UserActivityLevel"] = CalculateUserActivityLevel(Convert.ToInt32(record["UserId"]), data);
            }

            // Add item popularity (simplified)
            if (record.ContainsKey("ItemId"))
            {
                newRecord["ItemPopularity"] = CalculateItemPopularity(Convert.ToInt32(record["ItemId"]), data);
            }

            // Add time-based features
            if (record.ContainsKey("Timestamp") && record["Timestamp"] is DateTime timestamp)
            {
                newRecord["TimeOfDay"] = timestamp.Hour;
                newRecord["DayOfWeek"] = (int)timestamp.DayOfWeek;
                newRecord["IsWeekend"] = timestamp.DayOfWeek == DayOfWeek.Saturday || timestamp.DayOfWeek == DayOfWeek.Sunday;
            }

            // Add user preference score (simplified)
            newRecord["UserPreferenceScore"] = CalculateUserPreferenceScore(record);

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    private async Task<Dictionary<string, FeatureInfo>> GenerateFeatureMetadata(List<Dictionary<string, object>> data, List<string> featureColumns)
    {
        var metadata = new Dictionary<string, FeatureInfo>();

        foreach (var column in featureColumns)
        {
            var values = data.Where(d => d.ContainsKey(column) && d[column] != null)
                            .Select(d => d[column])
                            .ToList();

            if (values.Any())
            {
                var featureInfo = new FeatureInfo
                {
                    FeatureName = column,
                    Type = DetermineFeatureType(values.First()),
                    UniqueValueCount = values.Distinct().Count(),
                    SampleValues = values.Distinct().Take(5).ToList()
                };

                if (IsNumeric(values.First()))
                {
                    var numericValues = values.Select(v => Convert.ToDouble(v)).ToList();
                    featureInfo.MinValue = numericValues.Min();
                    featureInfo.MaxValue = numericValues.Max();
                    featureInfo.Mean = numericValues.Average();
                    featureInfo.StandardDeviation = CalculateStandardDeviation(numericValues);
                }

                metadata[column] = featureInfo;
            }
        }

        return metadata;
    }

    private async Task<FeatureImportanceScores> CalculateFeatureImportance(List<Dictionary<string, object>> data, string targetColumn, List<string> featureColumns)
    {
        // Simplified feature importance calculation using correlation
        var importanceScores = new Dictionary<string, double>();

        if (!data.Any() || !data.First().ContainsKey(targetColumn))
        {
            return new FeatureImportanceScores
            {
                Method = FeatureSelectionMethod.Correlation,
                Scores = importanceScores
            };
        }

        var targetValues = data.Where(d => d.ContainsKey(targetColumn) && d[targetColumn] != null)
                              .Select(d => Convert.ToDouble(d[targetColumn]))
                              .ToList();

        foreach (var feature in featureColumns)
        {
            if (data.First().ContainsKey(feature))
            {
                var featureValues = data.Where(d => d.ContainsKey(feature) && d[feature] != null)
                                       .Select(d => IsNumeric(d[feature]) ? Convert.ToDouble(d[feature]) : 0.0)
                                       .ToList();

                if (featureValues.Count == targetValues.Count && featureValues.Any())
                {
                    var correlation = CalculateCorrelation(targetValues, featureValues);
                    importanceScores[feature] = Math.Abs(correlation);
                }
                else
                {
                    importanceScores[feature] = 0.0;
                }
            }
        }

        var topFeatures = importanceScores.OrderByDescending(kvp => kvp.Value)
                                         .Take(10)
                                         .Select(kvp => kvp.Key)
                                         .ToList();

        return new FeatureImportanceScores
        {
            Method = FeatureSelectionMethod.Correlation,
            Scores = importanceScores,
            TopFeatures = topFeatures,
            TotalImportance = importanceScores.Values.Sum()
        };
    }

    private async Task<UserItemEmbeddings> CreateUserItemEmbeddings(PreprocessedInteractionData preprocessedData, RecommendationTrainingConfig config)
    {
        // Simplified embedding creation - in production, this would use actual embedding algorithms
        var embeddings = new UserItemEmbeddings
        {
            EmbeddingDimensions = config.EmbeddingDimensions,
            EmbeddingMethod = "Random" // Placeholder
        };

        var random = new Random(42); // Fixed seed for reproducibility

        // Create random embeddings for users
        foreach (var userId in preprocessedData.UserMapping.Values)
        {
            embeddings.UserEmbeddings[userId] = Enumerable.Range(0, config.EmbeddingDimensions)
                .Select(_ => random.NextDouble() * 2 - 1) // Random values between -1 and 1
                .ToList();
        }

        // Create random embeddings for items
        foreach (var itemId in preprocessedData.ItemMapping.Values)
        {
            embeddings.ItemEmbeddings[itemId] = Enumerable.Range(0, config.EmbeddingDimensions)
                .Select(_ => random.NextDouble() * 2 - 1)
                .ToList();
        }

        return embeddings;
    }

    private async Task<List<Dictionary<string, object>>> ApplyOneHotEncoding(List<Dictionary<string, object>> data, List<string> categoricalColumns)
    {
        var enrichedData = new List<Dictionary<string, object>>();

        // Get unique values for each categorical column
        var uniqueValues = new Dictionary<string, List<object>>();
        foreach (var column in categoricalColumns)
        {
            uniqueValues[column] = data.Where(d => d.ContainsKey(column) && d[column] != null)
                                      .Select(d => d[column])
                                      .Distinct()
                                      .ToList();
        }

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            foreach (var column in categoricalColumns)
            {
                if (record.ContainsKey(column) && record[column] != null)
                {
                    var value = record[column];
                    foreach (var uniqueValue in uniqueValues[column])
                    {
                        newRecord[$"{column}_{uniqueValue}"] = value.Equals(uniqueValue) ? 1 : 0;
                    }
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    private async Task<List<Dictionary<string, object>>> ApplyLabelEncoding(List<Dictionary<string, object>> data, List<string> categoricalColumns)
    {
        var enrichedData = new List<Dictionary<string, object>>();

        // Create label mappings
        var labelMappings = new Dictionary<string, Dictionary<object, int>>();
        foreach (var column in categoricalColumns)
        {
            var uniqueValues = data.Where(d => d.ContainsKey(column) && d[column] != null)
                                  .Select(d => d[column])
                                  .Distinct()
                                  .ToList();

            labelMappings[column] = uniqueValues.Select((value, index) => new { value, index })
                                               .ToDictionary(x => x.value, x => x.index);
        }

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            foreach (var column in categoricalColumns)
            {
                if (record.ContainsKey(column) && record[column] != null && labelMappings[column].ContainsKey(record[column]))
                {
                    newRecord[$"{column}_Encoded"] = labelMappings[column][record[column]];
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    private async Task<List<Dictionary<string, object>>> ApplyFrequencyEncoding(List<Dictionary<string, object>> data, List<string> categoricalColumns)
    {
        var enrichedData = new List<Dictionary<string, object>>();

        // Calculate frequency mappings
        var frequencyMappings = new Dictionary<string, Dictionary<object, int>>();
        foreach (var column in categoricalColumns)
        {
            frequencyMappings[column] = data.Where(d => d.ContainsKey(column) && d[column] != null)
                                           .GroupBy(d => d[column])
                                           .ToDictionary(g => g.Key, g => g.Count());
        }

        foreach (var record in data)
        {
            var newRecord = new Dictionary<string, object>(record);

            foreach (var column in categoricalColumns)
            {
                if (record.ContainsKey(column) && record[column] != null && frequencyMappings[column].ContainsKey(record[column]))
                {
                    newRecord[$"{column}_Frequency"] = frequencyMappings[column][record[column]];
                }
            }

            enrichedData.Add(newRecord);
        }

        return enrichedData;
    }

    private int GetWeekOfYear(DateTime date)
    {
        var jan1 = new DateTime(date.Year, 1, 1);
        var daysOffset = (int)jan1.DayOfWeek;
        var firstWeekDay = jan1.AddDays(-daysOffset);
        var weekNum = ((date - firstWeekDay).Days / 7) + 1;
        return weekNum;
    }

    private string GetSeason(DateTime date)
    {
        return date.Month switch
        {
            12 or 1 or 2 => "Winter",
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            9 or 10 or 11 => "Fall",
            _ => "Unknown"
        };
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count <= 1) return 0;

        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }

    private bool IsNumeric(object value)
    {
        return value is int || value is long || value is float || value is double || value is decimal;
    }

    private FeatureType DetermineFeatureType(object value)
    {
        return value switch
        {
            int or long or float or double or decimal => FeatureType.Numerical,
            bool => FeatureType.Boolean,
            DateTime => FeatureType.DateTime,
            string => FeatureType.Categorical,
            _ => FeatureType.Categorical
        };
    }

    private double CalculateUserActivityLevel(int userId, List<Dictionary<string, object>> data)
    {
        var userInteractions = data.Count(d => d.ContainsKey("UserId") && Convert.ToInt32(d["UserId"]) == userId);
        var totalInteractions = data.Count;
        return totalInteractions > 0 ? (double)userInteractions / totalInteractions : 0.0;
    }

    private double CalculateItemPopularity(int itemId, List<Dictionary<string, object>> data)
    {
        var itemInteractions = data.Count(d => d.ContainsKey("ItemId") && Convert.ToInt32(d["ItemId"]) == itemId);
        var totalInteractions = data.Count;
        return totalInteractions > 0 ? (double)itemInteractions / totalInteractions : 0.0;
    }

    private double CalculateUserPreferenceScore(Dictionary<string, object> record)
    {
        // Simplified preference score based on rating
        if (record.ContainsKey("Rating"))
        {
            return Convert.ToDouble(record["Rating"]) / 5.0; // Normalize to 0-1
        }
        return 0.5; // Default neutral preference
    }

    private double CalculateCorrelation(List<double> x, List<double> y)
    {
        if (x.Count != y.Count || x.Count == 0) return 0;

        var meanX = x.Average();
        var meanY = y.Average();

        var numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
        var denominatorX = Math.Sqrt(x.Sum(xi => Math.Pow(xi - meanX, 2)));
        var denominatorY = Math.Sqrt(y.Sum(yi => Math.Pow(yi - meanY, 2)));

        if (denominatorX == 0 || denominatorY == 0) return 0;

        return numerator / (denominatorX * denominatorY);
    }

    #endregion
}
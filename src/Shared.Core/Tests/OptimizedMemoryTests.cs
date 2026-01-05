using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Core.Services;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Shared.Core.Tests;

/// <summary>
/// Optimized memory tests that demonstrate memory monitoring without excessive allocations
/// These tests replace the memory-intensive tests with lightweight alternatives
/// </summary>
public class OptimizedMemoryTests
{
    private readonly ITestOutputHelper _output;
    private readonly ITestOptimizationService _testOptimizationService;

    public OptimizedMemoryTests(ITestOutputHelper output)
    {
        _output = output;
        
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<ITestOptimizationService, TestOptimizationService>();
        
        var serviceProvider = services.BuildServiceProvider();
        _testOptimizationService = serviceProvider.GetRequiredService<ITestOptimizationService>();
    }

    [Fact]
    public async Task MemoryMonitoring_ShouldDetectMemoryUsage()
    {
        // Arrange
        var testName = "MemoryMonitoring_Test";
        var memoryThresholdMB = 50; // 50MB threshold
        var memoryThresholdBytes = memoryThresholdMB * 1024 * 1024;
        var thresholdExceeded = false;

        // Act - Monitor memory during a lightweight operation
        using var monitor = _testOptimizationService.MonitorTestMemory(testName, info =>
        {
            _output.WriteLine($"Memory usage: {info.CurrentMemoryMB} at {info.Timestamp}");
            if (info.ThresholdExceeded)
            {
                thresholdExceeded = true;
                _output.WriteLine($"Memory threshold exceeded: {info.CurrentMemoryMB} > {memoryThresholdMB} MB");
            }
        });

        // Simulate some work without excessive memory allocation
        var data = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            data.Add($"Test data item {i}");
        }

        await Task.Delay(100); // Allow monitor to check memory

        // Assert
        Assert.NotNull(monitor);
        Assert.True(data.Count == 1000);
        
        // Memory threshold should not be exceeded for this lightweight test
        Assert.False(thresholdExceeded, "Memory threshold should not be exceeded for lightweight operations");
        
        _output.WriteLine($"Test completed successfully with {data.Count} items");
    }

    [Fact]
    public async Task LightweightAlternative_ShouldReplaceMemoryIntensiveOperation()
    {
        // Arrange
        var maxMemoryMB = 10;

        // Act - Use lightweight alternative instead of memory-intensive operation
        var result = await _testOptimizationService.CreateLightweightAlternativeAsync(async () =>
        {
            // Simulate work that would normally be memory-intensive
            var data = new List<object>();
            for (int i = 0; i < 100; i++)
            {
                // Use small objects instead of large byte arrays
                data.Add(new { Id = i, Name = $"Item {i}", Data = new string('x', 100) });
            }
            
            await Task.Delay(10); // Simulate processing time
            return data.Count;
        }, maxMemoryMB);

        // Assert
        Assert.Equal(100, result);
        _output.WriteLine($"Lightweight alternative completed successfully, processed {result} items");
    }

    [Fact]
    public async Task TestMemoryLimits_ShouldValidateMemoryUsage()
    {
        // Arrange
        var testName = "TestMemoryLimits_Validation";
        var memoryLimitBytes = 50 * 1024 * 1024; // 50MB limit

        // Act
        var withinLimits = await _testOptimizationService.ValidateTestMemoryLimitsAsync(testName, memoryLimitBytes);

        // Assert
        Assert.True(withinLimits, "Test should stay within memory limits");
        _output.WriteLine($"Memory validation passed for test: {testName}");
    }

    [Fact]
    public async Task AnalyzeMemoryUsage_ShouldIdentifyMemoryIntensiveTests()
    {
        // Act
        var analysisResult = await _testOptimizationService.AnalyzeMemoryUsageAsync();

        // Assert
        Assert.NotNull(analysisResult);
        Assert.NotEmpty(analysisResult.MemoryIntensiveTests);
        Assert.NotEmpty(analysisResult.Recommendations);
        Assert.True(analysisResult.AnalysisDuration.TotalMilliseconds > 0);

        _output.WriteLine($"Analysis found {analysisResult.MemoryIntensiveTests.Count} memory-intensive tests");
        _output.WriteLine($"Total estimated memory usage: {analysisResult.TotalMemoryUsage / 1024 / 1024} MB");
        
        foreach (var test in analysisResult.MemoryIntensiveTests)
        {
            _output.WriteLine($"- {test.TestName}: {test.MemoryUsageMB}");
            _output.WriteLine($"  Suggestion: {test.OptimizationSuggestion}");
        }

        foreach (var recommendation in analysisResult.Recommendations)
        {
            _output.WriteLine($"Recommendation: {recommendation}");
        }
    }

    [Fact]
    public async Task OptimizeMemoryIntensiveTests_ShouldProvideOptimizationResults()
    {
        // Act
        var optimizationResult = await _testOptimizationService.OptimizeMemoryIntensiveTestsAsync();

        // Assert
        Assert.NotNull(optimizationResult);
        Assert.True(optimizationResult.Success);
        Assert.True(optimizationResult.TestsOptimized > 0);
        Assert.True(optimizationResult.MemorySavedBytes > 0);
        Assert.NotEmpty(optimizationResult.OptimizedTests);

        _output.WriteLine($"Optimization completed: {optimizationResult.Message}");
        _output.WriteLine($"Tests optimized: {optimizationResult.TestsOptimized}");
        _output.WriteLine($"Memory saved: {optimizationResult.MemorySavedMB}");
        
        foreach (var testName in optimizationResult.OptimizedTests)
        {
            _output.WriteLine($"- Optimized: {testName}");
        }
    }

    [Fact]
    public async Task RunOptimizedTestSuite_ShouldExecuteWithMemoryMonitoring()
    {
        // Act
        var testSuiteResult = await _testOptimizationService.RunOptimizedTestSuiteAsync();

        // Assert
        Assert.NotNull(testSuiteResult);
        Assert.True(testSuiteResult.TotalTests > 0);
        Assert.True(testSuiteResult.PassedTests > 0);
        Assert.Equal(0, testSuiteResult.FailedTests); // All optimized tests should pass
        Assert.True(testSuiteResult.MaxMemoryUsageBytes < 100 * 1024 * 1024); // Should be under 100MB
        Assert.NotEmpty(testSuiteResult.TestResults);

        _output.WriteLine($"Test suite completed in {testSuiteResult.ExecutionTime.TotalMilliseconds}ms");
        _output.WriteLine($"Total tests: {testSuiteResult.TotalTests}, Passed: {testSuiteResult.PassedTests}, Failed: {testSuiteResult.FailedTests}");
        _output.WriteLine($"Maximum memory usage: {testSuiteResult.MaxMemoryUsageMB}");

        foreach (var testResult in testSuiteResult.TestResults)
        {
            _output.WriteLine($"- {testResult.TestName}: {(testResult.Passed ? "PASS" : "FAIL")} ({testResult.MemoryUsageMB}, {testResult.Duration.TotalMilliseconds}ms)");
        }
    }

    [Fact]
    public void StreamingApproach_ShouldProcessLargeDataWithoutMemorySpikes()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);
        var processedItems = 0;

        // Act - Use streaming approach instead of loading all data into memory
        var dataStream = GenerateDataStream(10000); // Generate 10,000 items
        
        foreach (var item in dataStream)
        {
            // Process each item individually without storing all in memory
            ProcessItem(item);
            processedItems++;
            
            // Periodically check memory usage
            if (processedItems % 1000 == 0)
            {
                var currentMemory = GC.GetTotalMemory(false);
                var memoryIncrease = currentMemory - initialMemory;
                
                // Memory increase should be minimal due to streaming
                Assert.True(memoryIncrease < 10 * 1024 * 1024, // Less than 10MB increase
                    $"Memory increase {memoryIncrease / 1024 / 1024}MB should be minimal with streaming approach");
            }
        }

        var finalMemory = GC.GetTotalMemory(false);
        var totalMemoryIncrease = finalMemory - initialMemory;

        // Assert
        Assert.Equal(10000, processedItems);
        Assert.True(totalMemoryIncrease < 20 * 1024 * 1024, // Less than 20MB total increase
            $"Total memory increase {totalMemoryIncrease / 1024 / 1024}MB should be minimal");

        _output.WriteLine($"Processed {processedItems} items using streaming approach");
        _output.WriteLine($"Total memory increase: {totalMemoryIncrease / 1024 / 1024}MB");
    }

    [Fact]
    public void PaginationApproach_ShouldHandleLargeDataSetsEfficiently()
    {
        // Arrange
        var totalItems = 10000;
        var pageSize = 100;
        var totalPages = (totalItems + pageSize - 1) / pageSize;
        var processedItems = 0;
        var maxMemoryUsage = 0L;
        var initialMemory = GC.GetTotalMemory(false);

        // Act - Process data in pages instead of all at once
        for (int page = 0; page < totalPages; page++)
        {
            var pageData = GetPageData(page, pageSize, totalItems);
            
            // Process the page
            foreach (var item in pageData)
            {
                ProcessItem(item);
                processedItems++;
            }

            // Check memory usage after each page
            var currentMemory = GC.GetTotalMemory(false);
            var memoryUsed = currentMemory - initialMemory;
            maxMemoryUsage = Math.Max(maxMemoryUsage, memoryUsed);

            // Force garbage collection after each page to keep memory usage low
            if (page % 10 == 0) // Every 10 pages
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Assert
        Assert.Equal(totalItems, processedItems);
        Assert.True(maxMemoryUsage < 50 * 1024 * 1024, // Less than 50MB max usage
            $"Maximum memory usage {maxMemoryUsage / 1024 / 1024}MB should be controlled with pagination");

        _output.WriteLine($"Processed {processedItems} items using pagination approach ({pageSize} items per page)");
        _output.WriteLine($"Maximum memory usage: {maxMemoryUsage / 1024 / 1024}MB");
    }

    private static IEnumerable<string> GenerateDataStream(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return $"Data item {i}";
        }
    }

    private static List<string> GetPageData(int page, int pageSize, int totalItems)
    {
        var startIndex = page * pageSize;
        var endIndex = Math.Min(startIndex + pageSize, totalItems);
        var pageData = new List<string>();

        for (int i = startIndex; i < endIndex; i++)
        {
            pageData.Add($"Page data item {i}");
        }

        return pageData;
    }

    private static void ProcessItem(string item)
    {
        // Simulate item processing without storing the item
        var hash = item.GetHashCode();
        // Do something with the hash to prevent optimization
        _ = hash.ToString();
    }
}
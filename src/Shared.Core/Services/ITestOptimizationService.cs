using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared.Core.Services;

/// <summary>
/// Service for optimizing memory-intensive tests and monitoring test memory usage
/// </summary>
public interface ITestOptimizationService
{
    /// <summary>
    /// Analyzes memory usage patterns in tests
    /// </summary>
    Task<TestAnalysisResult> AnalyzeMemoryUsageAsync();
    
    /// <summary>
    /// Optimizes memory-intensive tests by replacing large allocations with streaming approaches
    /// </summary>
    Task<OptimizationResult> OptimizeMemoryIntensiveTestsAsync();
    
    /// <summary>
    /// Validates that a test stays within memory limits during execution
    /// </summary>
    Task<bool> ValidateTestMemoryLimitsAsync(string testName, long memoryLimitBytes);
    
    /// <summary>
    /// Runs the optimized test suite with memory monitoring
    /// </summary>
    Task<TestSuiteResult> RunOptimizedTestSuiteAsync();
    
    /// <summary>
    /// Creates a lightweight alternative to memory-intensive operations
    /// </summary>
    Task<T> CreateLightweightAlternativeAsync<T>(Func<Task<T>> memoryIntensiveOperation, int maxMemoryMB = 10);
    
    /// <summary>
    /// Monitors memory usage during test execution
    /// </summary>
    IDisposable MonitorTestMemory(string testName, Action<MemoryUsageInfo> onMemoryThresholdExceeded);
}

/// <summary>
/// Result of test memory analysis
/// </summary>
public class TestAnalysisResult
{
    public List<MemoryIntensiveTest> MemoryIntensiveTests { get; set; } = new();
    public long TotalMemoryUsage { get; set; }
    public TimeSpan AnalysisDuration { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Information about a memory-intensive test
/// </summary>
public class MemoryIntensiveTest
{
    public string TestName { get; set; } = string.Empty;
    public long MemoryUsageBytes { get; set; }
    public string MemoryUsageMB => $"{MemoryUsageBytes / 1024.0 / 1024.0:F2} MB";
    public List<string> LargeAllocations { get; set; } = new();
    public string OptimizationSuggestion { get; set; } = string.Empty;
}

/// <summary>
/// Result of test optimization
/// </summary>
public class OptimizationResult
{
    public int TestsOptimized { get; set; }
    public long MemorySavedBytes { get; set; }
    public string MemorySavedMB => $"{MemorySavedBytes / 1024.0 / 1024.0:F2} MB";
    public List<string> OptimizedTests { get; set; } = new();
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of running the optimized test suite
/// </summary>
public class TestSuiteResult
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public long MaxMemoryUsageBytes { get; set; }
    public string MaxMemoryUsageMB => $"{MaxMemoryUsageBytes / 1024.0 / 1024.0:F2} MB";
    public TimeSpan ExecutionTime { get; set; }
    public List<TestResult> TestResults { get; set; } = new();
}

/// <summary>
/// Individual test result with memory information
/// </summary>
public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public long MemoryUsageBytes { get; set; }
    public string MemoryUsageMB => $"{MemoryUsageBytes / 1024.0 / 1024.0:F2} MB";
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Memory usage information during test execution
/// </summary>
public class MemoryUsageInfo
{
    public string TestName { get; set; } = string.Empty;
    public long CurrentMemoryBytes { get; set; }
    public string CurrentMemoryMB => $"{CurrentMemoryBytes / 1024.0 / 1024.0:F2} MB";
    public long ThresholdBytes { get; set; }
    public DateTime Timestamp { get; set; }
    public bool ThresholdExceeded => CurrentMemoryBytes > ThresholdBytes;
}
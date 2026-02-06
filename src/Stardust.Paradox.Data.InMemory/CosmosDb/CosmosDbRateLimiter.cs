using System;
using System.Threading;
using Stardust.Paradox.Data.InMemory.Core;

namespace Stardust.Paradox.Data.InMemory.CosmosDb;

/// <summary>
/// Interface for rate limiting in Cosmos DB emulation mode
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Check if request can proceed, returns result with delay if rate limited
    /// </summary>
    RateLimitResult TryConsume(double requestUnits);
    
    /// <summary>
    /// Reset the rate limiter (for testing)
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Get current available RUs
    /// </summary>
    double AvailableRU { get; }
    
    /// <summary>
    /// Get max RU per second
    /// </summary>
    double MaxRUPerSecond { get; }
}

/// <summary>
/// Simulates Azure Cosmos DB rate limiting behavior with token bucket algorithm
/// </summary>
public class CosmosDbRateLimiter : IRateLimiter
{
    private readonly double _maxRUPerSecond;
    private double _availableRU;
    private DateTime _lastReplenish;
    private readonly object _lock = new object();
    
    public double AvailableRU
    {
        get
        {
            lock (_lock)
            {
                ReplenishIfNeeded();
                return _availableRU;
            }
        }
    }
    
    public double MaxRUPerSecond => _maxRUPerSecond;
    
    public CosmosDbRateLimiter(double maxRUPerSecond)
    {
        _maxRUPerSecond = maxRUPerSecond > 0 ? maxRUPerSecond : 10000;
        _availableRU = _maxRUPerSecond;
        _lastReplenish = DateTime.UtcNow;
    }
    
    public CosmosDbRateLimiter(InMemoryDatabaseOptions options)
        : this(options?.MaxRequestUnitsPerSecond ?? 10000)
    {
    }
    
    /// <summary>
    /// Try to consume RUs for an operation
    /// </summary>
    public RateLimitResult TryConsume(double requestUnits)
    {
        if (requestUnits <= 0)
            return RateLimitResult.Allowed(0);
        
        lock (_lock)
        {
            ReplenishIfNeeded();
            
            if (_availableRU >= requestUnits)
            {
                _availableRU -= requestUnits;
                return RateLimitResult.Allowed(requestUnits);
            }
            
            // Calculate retry-after delay based on deficit
            var deficit = requestUnits - _availableRU;
            var retryAfterMs = (int)Math.Ceiling((deficit / _maxRUPerSecond) * 1000);
            
            // Minimum retry after is 1ms, maximum is 60 seconds
            retryAfterMs = Math.Max(1, Math.Min(retryAfterMs, 60000));
            
            return RateLimitResult.RateLimited(retryAfterMs, requestUnits, _availableRU);
        }
    }
    
    /// <summary>
    /// Reset the rate limiter to full capacity
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _availableRU = _maxRUPerSecond;
            _lastReplenish = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Replenish available RUs based on elapsed time
    /// </summary>
    private void ReplenishIfNeeded()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastReplenish).TotalSeconds;
        
        if (elapsed > 0)
        {
            var replenishAmount = elapsed * _maxRUPerSecond;
            _availableRU = Math.Min(_maxRUPerSecond, _availableRU + replenishAmount);
            _lastReplenish = now;
        }
    }
}

/// <summary>
/// Result of a rate limit check
/// </summary>
public class RateLimitResult
{
    /// <summary>
    /// Whether the operation is allowed to proceed
    /// </summary>
    public bool IsAllowed { get; }
    
    /// <summary>
    /// RUs that were/would be consumed
    /// </summary>
    public double RequestedRU { get; }
    
    /// <summary>
    /// RUs that were actually consumed (0 if rate limited)
    /// </summary>
    public double ConsumedRU { get; }
    
    /// <summary>
    /// Available RUs at time of request
    /// </summary>
    public double AvailableRU { get; }
    
    /// <summary>
    /// Milliseconds to wait before retrying (only if rate limited)
    /// </summary>
    public int RetryAfterMs { get; }
    
    /// <summary>
    /// Human-readable message
    /// </summary>
    public string Message { get; }
    
    private RateLimitResult(bool isAllowed, double requestedRU, double consumedRU, 
        double availableRU, int retryAfterMs, string message)
    {
        IsAllowed = isAllowed;
        RequestedRU = requestedRU;
        ConsumedRU = consumedRU;
        AvailableRU = availableRU;
        RetryAfterMs = retryAfterMs;
        Message = message;
    }
    
    /// <summary>
    /// Create result for allowed operation
    /// </summary>
    public static RateLimitResult Allowed(double consumedRU)
    {
        return new RateLimitResult(
            isAllowed: true,
            requestedRU: consumedRU,
            consumedRU: consumedRU,
            availableRU: 0, // Not relevant when allowed
            retryAfterMs: 0,
            message: null);
    }
    
    /// <summary>
    /// Create result for rate limited operation
    /// </summary>
    public static RateLimitResult RateLimited(int retryAfterMs, double requestedRU = 0, double availableRU = 0)
    {
        return new RateLimitResult(
            isAllowed: false,
            requestedRU: requestedRU,
            consumedRU: 0,
            availableRU: availableRU,
            retryAfterMs: retryAfterMs,
            message: $"Request rate is large. More Request Units may be needed. " +
                    $"Requested: {requestedRU:F2} RU, Available: {availableRU:F2} RU. " +
                    $"Please retry after {retryAfterMs}ms.");
    }
}

/// <summary>
/// Calculates RU costs for different operations based on Cosmos DB pricing model
/// </summary>
public class RUCostCalculator
{
    private readonly CosmosDbRUCostSettings _settings;
    
    public RUCostCalculator(CosmosDbRUCostSettings settings)
    {
        _settings = settings ?? new CosmosDbRUCostSettings();
    }
    
    /// <summary>
    /// Calculate RU cost for a read operation
    /// </summary>
    public double CalculateReadCost(int itemCount, long totalSizeBytes, bool isCrossPartition = false)
    {
        var baseCost = _settings.PointReadRU * itemCount;
        var sizeCost = (totalSizeBytes / 1024.0) * _settings.ReadPerKBRU;
        var totalCost = baseCost + sizeCost;
        
        if (isCrossPartition)
        {
            totalCost *= _settings.CrossPartitionMultiplier;
        }
        
        return Math.Max(1.0, totalCost);
    }
    
    /// <summary>
    /// Calculate RU cost for vertex creation
    /// </summary>
    public double CalculateVertexCreateCost(long sizeBytes)
    {
        var baseCost = _settings.CreateVertexRU;
        var sizeCost = (sizeBytes / 1024.0) * _settings.WritePerKBRU;
        return Math.Max(baseCost, baseCost + sizeCost);
    }
    
    /// <summary>
    /// Calculate RU cost for edge creation
    /// </summary>
    public double CalculateEdgeCreateCost(long sizeBytes)
    {
        var baseCost = _settings.CreateEdgeRU;
        var sizeCost = (sizeBytes / 1024.0) * _settings.WritePerKBRU;
        return Math.Max(baseCost, baseCost + sizeCost);
    }
    
    /// <summary>
    /// Calculate RU cost for a traversal query
    /// </summary>
    public double CalculateTraversalCost(int stepCount, int resultCount, long totalSizeBytes, bool isCrossPartition = false)
    {
        var stepCost = stepCount * _settings.TraversalStepRU;
        var readCost = CalculateReadCost(resultCount, totalSizeBytes, isCrossPartition);
        return stepCost + readCost;
    }
    
    /// <summary>
    /// Calculate RU cost for a delete operation
    /// </summary>
    public double CalculateDeleteCost(bool isVertex, int count = 1)
    {
        return isVertex 
            ? _settings.DeleteVertexRU * count 
            : _settings.DeleteEdgeRU * count;
    }
    
    /// <summary>
    /// Calculate RU cost for an update operation
    /// </summary>
    public double CalculateUpdateCost(bool isVertex, long sizeBytes)
    {
        var baseCost = isVertex ? _settings.UpdateVertexRU : _settings.UpdateEdgeRU;
        var sizeCost = (sizeBytes / 1024.0) * _settings.WritePerKBRU;
        return Math.Max(baseCost, baseCost + sizeCost);
    }
    
    /// <summary>
    /// Calculate RU cost for an aggregation operation
    /// </summary>
    public double CalculateAggregationCost(int itemsProcessed, bool isCrossPartition = false)
    {
        var baseCost = _settings.AggregationStepRU;
        var itemCost = itemsProcessed * 0.1; // Small cost per item processed
        var totalCost = baseCost + itemCost;
        
        if (isCrossPartition)
        {
            totalCost *= _settings.CrossPartitionMultiplier;
        }
        
        return Math.Max(1.0, totalCost);
    }
}

# Performance Considerations

This guide covers performance implications and optimization strategies when using Plugin.ExceptionListeners in your applications.

## Overview

Exception listeners have different performance characteristics depending on their type and frequency of activation:

| Listener Type | Frequency | Performance Impact | Optimization Priority |
|--------------|-----------|-------------------|---------------------|
| **FirstChanceException** | Very High | High | Critical |
| **UnhandledException** | Very Low | Low | Low |
| **UnobservedTaskException** | Low-Medium | Medium | Medium |
| **NativeUnhandledException** | Low | Low-Medium | Medium |

## First-Chance Exception Performance

### Understanding the Impact

First-chance exception listeners are called for **every exception** that occurs in your application, including those that are caught and handled normally. In a typical .NET application, this can be thousands of exceptions per second.

### Performance Measurement

```csharp
public class PerformanceMeasurement
{
    private static long _handlerCallCount = 0;
    private static long _totalHandlerTime = 0;

    public void MeasureHandlerPerformance()
    {
        var listener = new CurrentDomainFirstChanceExceptionListener(MeasuredHandler);

        // Run your application and monitor
        var timer = new Timer(ReportPerformance, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private void MeasuredHandler(object? sender, ExceptionEventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Your actual handler logic here
            HandleExceptionFast(e.Exception);
        }
        finally
        {
            stopwatch.Stop();
            Interlocked.Increment(ref _handlerCallCount);
            Interlocked.Add(ref _totalHandlerTime, stopwatch.ElapsedTicks);
        }
    }

    private void ReportPerformance(object? state)
    {
        var calls = Interlocked.Read(ref _handlerCallCount);
        var totalTicks = Interlocked.Read(ref _totalHandlerTime);

        if (calls > 0)
        {
            var avgMicroseconds = (totalTicks * 1000000.0) / (calls * Stopwatch.Frequency);
            Console.WriteLine($"Average handler time: {avgMicroseconds:F2} μs over {calls} calls");

            // Reset for next period
            Interlocked.Exchange(ref _handlerCallCount, 0);
            Interlocked.Exchange(ref _totalHandlerTime, 0);
        }
    }

    private void HandleExceptionFast(Exception exception)
    {
        // Keep this extremely fast - sub-microsecond if possible
    }
}
```

### Optimization Strategies

#### 1. Ultra-Lightweight Handlers

```csharp
public class OptimizedFirstChanceHandler
{
    // Use simple, fast data structures
    private static readonly ConcurrentDictionary<int, int> ExceptionTypeCounts = new();
    private static long _totalExceptions = 0;

    public void HandleFirstChanceOptimized(object? sender, ExceptionEventArgs e)
    {
        // Ultra-fast operations only

        // Option 1: Just count total exceptions
        Interlocked.Increment(ref _totalExceptions);

        // Option 2: Count by type hash (faster than string operations)
        var typeHash = e.Exception.GetType().GetHashCode();
        ExceptionTypeCounts.AddOrUpdate(typeHash, 1, (k, v) => v + 1);

        // Option 3: Sample-based approach (only process every Nth exception)
        if (_totalExceptions % 100 == 0) // Only process 1% of exceptions
        {
            SampleException(e.Exception);
        }
    }

    private void SampleException(Exception exception)
    {
        // More detailed processing for sampled exceptions
        var info = new ExceptionSample
        {
            TypeName = exception.GetType().Name,
            Timestamp = DateTimeOffset.UtcNow,
            Message = exception.Message?.Substring(0, Math.Min(100, exception.Message.Length))
        };

        // Queue for background processing
        BackgroundProcessor.Enqueue(info);
    }

    public class ExceptionSample
    {
        public string TypeName { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string? Message { get; set; }
    }
}
```

#### 2. Lock-Free Data Structures

```csharp
public class LockFreeExceptionCounter
{
    // Use arrays for better cache locality and lock-free operations
    private static readonly int[] ExceptionCounts = new int[256]; // Fixed-size array
    private static readonly string[] TypeNames = new string[256]; // Parallel array for names
    private static int _nextIndex = 0;

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var typeName = e.Exception.GetType().Name;
        var hash = typeName.GetHashCode();
        var index = Math.Abs(hash) % ExceptionCounts.Length;

        // Atomic increment
        Interlocked.Increment(ref ExceptionCounts[index]);

        // Store type name if not already stored (racy but acceptable)
        if (TypeNames[index] == null)
        {
            TypeNames[index] = typeName;
        }
    }

    public void ReportCounts()
    {
        for (int i = 0; i < ExceptionCounts.Length; i++)
        {
            var count = ExceptionCounts[i];
            if (count > 0 && TypeNames[i] != null)
            {
                Console.WriteLine($"{TypeNames[i]}: {count}");
            }
        }
    }
}
```

#### 3. Conditional Compilation

```csharp
public class ConditionalExceptionHandler
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HandleFirstChance(object? sender, ExceptionEventArgs e)
    {
        #if EXCEPTION_MONITORING
        ProcessException(e.Exception);
        #endif

        #if PERFORMANCE_COUNTERS
        Interlocked.Increment(ref GlobalCounters.FirstChanceExceptions);
        #endif

        #if DEBUG
        DebugOutput(e.Exception);
        #endif
    }

    #if EXCEPTION_MONITORING
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ProcessException(Exception exception)
    {
        // More expensive processing only when monitoring is enabled
        ExceptionProcessor.Process(exception);
    }
    #endif

    #if DEBUG
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DebugOutput(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine($"Exception: {exception.GetType().Name}");
    }
    #endif
}
```

## Asynchronous Processing

### Background Processing Pipeline

```csharp
public class AsyncExceptionProcessor : IDisposable
{
    private readonly Channel<ExceptionEvent> _channel;
    private readonly ChannelWriter<ExceptionEvent> _writer;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public AsyncExceptionProcessor()
    {
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // Drop old items under load
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false // Don't block producers
        };

        _channel = Channel.CreateBounded<ExceptionEvent>(options);
        _writer = _channel.Writer;
        _processingTask = Task.Run(ProcessExceptionsAsync, _cancellationTokenSource.Token);
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        // Ultra-fast: just queue the exception
        var evt = new ExceptionEvent
        {
            Exception = e.Exception,
            Source = sender?.GetType().Name,
            Timestamp = DateTimeOffset.UtcNow.Ticks // Faster than UtcNow
        };

        // Non-blocking write
        if (!_writer.TryWrite(evt))
        {
            // Channel full - increment dropped counter
            Interlocked.Increment(ref _droppedExceptions);
        }
    }

    private async Task ProcessExceptionsAsync()
    {
        var batchSize = 100;
        var batch = new List<ExceptionEvent>(batchSize);

        await foreach (var evt in _channel.Reader.ReadAllAsync(_cancellationTokenSource.Token))
        {
            batch.Add(evt);

            if (batch.Count >= batchSize ||
                (_channel.Reader.TryPeek(out _) == false && batch.Count > 0))
            {
                await ProcessBatch(batch);
                batch.Clear();
            }
        }
    }

    private async Task ProcessBatch(List<ExceptionEvent> events)
    {
        try
        {
            // Group similar exceptions for efficient processing
            var grouped = events.GroupBy(e => new
            {
                Type = e.Exception.GetType().Name,
                Source = e.Source
            });

            foreach (var group in grouped)
            {
                var count = group.Count();
                var sample = group.First();

                await LogExceptionGroup(group.Key.Type, group.Key.Source, count, sample);
            }
        }
        catch (Exception ex)
        {
            // Don't let batch processing failures stop the processor
            System.Diagnostics.Debug.WriteLine($"Batch processing failed: {ex}");
        }
    }

    private async Task LogExceptionGroup(string type, string? source, int count, ExceptionEvent sample)
    {
        // Implement your logging logic here
        await Task.CompletedTask; // Placeholder
    }

    private static long _droppedExceptions = 0;

    public void Dispose()
    {
        _writer.Complete();
        _cancellationTokenSource.Cancel();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        _cancellationTokenSource.Dispose();
    }

    private class ExceptionEvent
    {
        public Exception Exception { get; set; } = null!;
        public string? Source { get; set; }
        public long Timestamp { get; set; }
    }
}
```

### Batch Processing with Backpressure

```csharp
public class BackpressureAwareProcessor
{
    private readonly SemaphoreSlim _processingThrottle;
    private readonly Timer _metricTimer;
    private long _processedCount = 0;
    private long _droppedCount = 0;

    public BackpressureAwareProcessor()
    {
        // Limit concurrent processing to prevent resource exhaustion
        _processingThrottle = new SemaphoreSlim(Environment.ProcessorCount * 2);

        // Report metrics periodically
        _metricTimer = new Timer(ReportMetrics, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async void HandleException(object? sender, ExceptionEventArgs e)
    {
        // Check if we can process this exception without blocking
        if (_processingThrottle.Wait(0)) // Non-blocking check
        {
            // Process asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessExceptionAsync(e.Exception);
                    Interlocked.Increment(ref _processedCount);
                }
                catch
                {
                    // Log processing failure if needed
                }
                finally
                {
                    _processingThrottle.Release();
                }
            });
        }
        else
        {
            // Under load - drop this exception
            Interlocked.Increment(ref _droppedCount);
        }
    }

    private async Task ProcessExceptionAsync(Exception exception)
    {
        // Simulate processing work
        await Task.Delay(10); // Placeholder for actual work
    }

    private void ReportMetrics(object? state)
    {
        var processed = Interlocked.Exchange(ref _processedCount, 0);
        var dropped = Interlocked.Exchange(ref _droppedCount, 0);

        var total = processed + dropped;
        if (total > 0)
        {
            var dropRate = (double)dropped / total * 100;
            Console.WriteLine($"Processed: {processed}, Dropped: {dropped} ({dropRate:F1}% drop rate)");
        }
    }
}
```

## Memory Optimization

### Object Pooling

```csharp
public class PooledExceptionHandler
{
    private readonly ObjectPool<ExceptionInfo> _exceptionInfoPool;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;

    public PooledExceptionHandler()
    {
        var exceptionInfoPolicy = new ExceptionInfoPoolPolicy();
        var stringBuilderPolicy = new StringBuilderPooledObjectPolicy();

        _exceptionInfoPool = new DefaultObjectPool<ExceptionInfo>(exceptionInfoPolicy);
        _stringBuilderPool = new DefaultObjectPool<StringBuilder>(stringBuilderPolicy);
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var info = _exceptionInfoPool.Get();
        var sb = _stringBuilderPool.Get();

        try
        {
            // Populate pooled objects
            info.Exception = e.Exception;
            info.Source = sender?.GetType().Name;
            info.Timestamp = DateTimeOffset.UtcNow;

            // Use pooled StringBuilder for string operations
            sb.Clear();
            sb.Append(info.Source);
            sb.Append(": ");
            sb.Append(e.Exception.GetType().Name);

            // Process the exception info
            ProcessExceptionInfo(info, sb.ToString());
        }
        finally
        {
            // Return objects to pool
            info.Reset();
            _exceptionInfoPool.Return(info);
            _stringBuilderPool.Return(sb);
        }
    }

    private void ProcessExceptionInfo(ExceptionInfo info, string formattedMessage)
    {
        // Process the exception with minimal allocations
        Console.WriteLine(formattedMessage);
    }

    public class ExceptionInfo
    {
        public Exception? Exception { get; set; }
        public string? Source { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public void Reset()
        {
            Exception = null;
            Source = null;
            Timestamp = default;
        }
    }

    public class ExceptionInfoPoolPolicy : IPooledObjectPolicy<ExceptionInfo>
    {
        public ExceptionInfo Create() => new ExceptionInfo();

        public bool Return(ExceptionInfo obj)
        {
            obj.Reset();
            return true;
        }
    }
}
```

### Memory-Efficient Aggregation

```csharp
public class MemoryEfficientAggregator
{
    // Use structs for value types to reduce GC pressure
    private readonly ConcurrentDictionary<ExceptionKey, ExceptionStats> _stats = new();
    private readonly Timer _cleanupTimer;

    public MemoryEfficientAggregator()
    {
        _cleanupTimer = new Timer(CleanupOldStats, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        var key = new ExceptionKey(
            e.Exception.GetType().GetHashCode(),
            e.Exception.Message?.GetHashCode() ?? 0);

        _stats.AddOrUpdate(key,
            new ExceptionStats { Count = 1, LastSeen = DateTimeOffset.UtcNow },
            (k, existing) => new ExceptionStats
            {
                Count = existing.Count + 1,
                LastSeen = DateTimeOffset.UtcNow
            });
    }

    private void CleanupOldStats(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);
        var keysToRemove = new List<ExceptionKey>();

        foreach (var kvp in _stats)
        {
            if (kvp.Value.LastSeen < cutoff)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _stats.TryRemove(key, out _);
        }

        // Force GC if we removed a lot of items
        if (keysToRemove.Count > 1000)
        {
            GC.Collect(0, GCCollectionMode.Optimized);
        }
    }

    // Use struct for keys to reduce allocations
    private readonly struct ExceptionKey : IEquatable<ExceptionKey>
    {
        private readonly int _typeHash;
        private readonly int _messageHash;

        public ExceptionKey(int typeHash, int messageHash)
        {
            _typeHash = typeHash;
            _messageHash = messageHash;
        }

        public bool Equals(ExceptionKey other) =>
            _typeHash == other._typeHash && _messageHash == other._messageHash;

        public override bool Equals(object? obj) =>
            obj is ExceptionKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(_typeHash, _messageHash);
    }

    private struct ExceptionStats
    {
        public int Count { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}
```

## Profiling and Monitoring

### Performance Monitoring

```csharp
public class PerformanceMonitor
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly Timer _monitoringTimer;

    public PerformanceMonitor()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");

            _monitoringTimer = new Timer(CheckPerformance, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
        catch (Exception ex)
        {
            // Performance counters might not be available
            Console.WriteLine($"Performance monitoring unavailable: {ex.Message}");
        }
    }

    private void CheckPerformance(object? state)
    {
        try
        {
            var cpuUsage = _cpuCounter?.NextValue() ?? 0;
            var availableMemory = _memoryCounter?.NextValue() ?? 0;

            // Adjust exception handling based on system load
            if (cpuUsage > 80 || availableMemory < 100)
            {
                // System under stress - reduce exception processing
                ExceptionHandlingConfig.ReducedMode = true;
            }
            else
            {
                ExceptionHandlingConfig.ReducedMode = false;
            }

            // Report metrics
            Console.WriteLine($"CPU: {cpuUsage:F1}%, Available Memory: {availableMemory:F0}MB");
        }
        catch
        {
            // Ignore monitoring failures
        }
    }
}

public static class ExceptionHandlingConfig
{
    public static volatile bool ReducedMode = false;
}
```

### Adaptive Processing

```csharp
public class AdaptiveExceptionProcessor
{
    private readonly RateLimiter _rateLimiter;
    private int _currentThroughput = 1000; // Start with 1000 exceptions/second

    public AdaptiveExceptionProcessor()
    {
        _rateLimiter = new RateLimiter(_currentThroughput);

        // Adjust throughput based on system performance
        var adaptationTimer = new Timer(AdaptThroughput, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public void HandleException(object? sender, ExceptionEventArgs e)
    {
        if (_rateLimiter.TryAcquire())
        {
            ProcessException(e.Exception);
        }
        else
        {
            // Rate limited - drop this exception
            Interlocked.Increment(ref DroppedExceptions);
        }
    }

    private void AdaptThroughput(object? state)
    {
        if (ExceptionHandlingConfig.ReducedMode)
        {
            // Reduce throughput under load
            _currentThroughput = Math.Max(100, _currentThroughput / 2);
        }
        else
        {
            // Increase throughput when system is healthy
            _currentThroughput = Math.Min(10000, _currentThroughput * 2);
        }

        _rateLimiter.UpdateRate(_currentThroughput);
    }

    private static long DroppedExceptions = 0;

    private void ProcessException(Exception exception)
    {
        // Process the exception
    }
}

public class RateLimiter
{
    private int _maxPerSecond;
    private long _lastSecond;
    private int _currentCount;

    public RateLimiter(int maxPerSecond)
    {
        _maxPerSecond = maxPerSecond;
        _lastSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public bool TryAcquire()
    {
        var currentSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (currentSecond != _lastSecond)
        {
            Interlocked.Exchange(ref _currentCount, 0);
            Interlocked.Exchange(ref _lastSecond, currentSecond);
        }

        return Interlocked.Increment(ref _currentCount) <= _maxPerSecond;
    }

    public void UpdateRate(int maxPerSecond)
    {
        _maxPerSecond = maxPerSecond;
    }
}
```

## Benchmarking

### Performance Benchmarks

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ExceptionHandlerBenchmarks
{
    private Exception _testException;
    private ExceptionEventArgs _testEventArgs;

    [GlobalSetup]
    public void Setup()
    {
        _testException = new InvalidOperationException("Test exception message for benchmarking");
        _testEventArgs = new ExceptionEventArgs(_testException);
    }

    [Benchmark(Baseline = true)]
    public void NoOperation()
    {
        // Baseline - do nothing
    }

    [Benchmark]
    public void SimpleCounter()
    {
        Interlocked.Increment(ref SimpleCounterValue);
    }

    [Benchmark]
    public void ConcurrentDictionaryUpdate()
    {
        var key = _testException.GetType().Name;
        ConcurrentDict.AddOrUpdate(key, 1, (k, v) => v + 1);
    }

    [Benchmark]
    public void StructKeyDictionaryUpdate()
    {
        var key = new StructKey(_testException.GetType().GetHashCode());
        StructDict.AddOrUpdate(key, 1, (k, v) => v + 1);
    }

    [Benchmark]
    public void ArrayBasedCounter()
    {
        var index = Math.Abs(_testException.GetType().GetHashCode()) % ArrayCounters.Length;
        Interlocked.Increment(ref ArrayCounters[index]);
    }

    [Benchmark]
    public void QueueBasedProcessing()
    {
        if (ExceptionQueue.Count < 10000) // Prevent unbounded growth
        {
            ExceptionQueue.Enqueue(_testException);
        }
    }

    private static long SimpleCounterValue = 0;
    private static readonly ConcurrentDictionary<string, int> ConcurrentDict = new();
    private static readonly ConcurrentDictionary<StructKey, int> StructDict = new();
    private static readonly int[] ArrayCounters = new int[256];
    private static readonly ConcurrentQueue<Exception> ExceptionQueue = new();

    private readonly struct StructKey
    {
        private readonly int _hash;
        public StructKey(int hash) => _hash = hash;
        public override int GetHashCode() => _hash;
        public override bool Equals(object? obj) => obj is StructKey other && _hash == other._hash;
    }
}
```

### Load Testing

```csharp
public class LoadTest
{
    public async Task SimulateHighExceptionLoad()
    {
        var listener = new CurrentDomainFirstChanceExceptionListener(OptimizedHandler);
        var tasks = new List<Task>();

        // Create load across multiple threads
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            tasks.Add(Task.Run(GenerateExceptions));
        }

        // Run for 30 seconds
        await Task.Delay(TimeSpan.FromSeconds(30));

        // Report results
        Console.WriteLine($"Handled {TotalExceptions} exceptions");

        listener.Dispose();
    }

    private async Task GenerateExceptions()
    {
        var random = new Random();

        while (!CancellationToken.IsCancellationRequested)
        {
            try
            {
                // Generate different types of exceptions
                switch (random.Next(4))
                {
                    case 0:
                        throw new InvalidOperationException("Load test exception 1");
                    case 1:
                        throw new ArgumentException("Load test exception 2");
                    case 2:
                        throw new NotSupportedException("Load test exception 3");
                    default:
                        throw new ApplicationException("Load test exception 4");
                }
            }
            catch
            {
                // Exceptions are caught - will trigger first-chance listener
            }

            // Small delay to prevent overwhelming the system
            if (random.Next(100) == 0)
            {
                await Task.Delay(1);
            }
        }
    }

    private static long TotalExceptions = 0;

    private void OptimizedHandler(object? sender, ExceptionEventArgs e)
    {
        Interlocked.Increment(ref TotalExceptions);
    }
}
```

## Summary

Key performance considerations:

1. **First-chance handlers must be ultra-fast** - aim for sub-microsecond execution
2. **Use asynchronous processing** for heavy operations
3. **Implement proper backpressure** to handle load spikes
4. **Monitor system resources** and adapt processing accordingly
5. **Use object pooling** to reduce GC pressure
6. **Benchmark your handlers** to ensure they meet performance requirements
7. **Test under load** to validate performance in realistic scenarios

By following these performance guidelines, you can implement comprehensive exception monitoring without significantly impacting your application's performance.

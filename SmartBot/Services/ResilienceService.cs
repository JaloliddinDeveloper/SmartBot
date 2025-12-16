using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using SmartBot.Common;
using Telegram.Bot.Exceptions;

namespace SmartBot.Services;

/// <summary>
/// Resilience service providing retry logic and circuit breaker patterns
/// Uses Polly library for robust error handling
/// </summary>
public interface IResilienceService
{
    /// <summary>
    /// Executes operation with retry logic
    /// </summary>
    Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes operation with circuit breaker
    /// </summary>
    Task<T> ExecuteWithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes operation with full resilience (retry + circuit breaker)
    /// </summary>
    Task<T> ExecuteWithFullResilienceAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets circuit breaker state
    /// </summary>
    CircuitState GetCircuitState();
}

public class ResilienceService : IResilienceService
{
    private readonly ILogger<ResilienceService> _logger;
    private readonly ResiliencePipeline<object> _retryPipeline;
    private readonly ResiliencePipeline<object> _circuitBreakerPipeline;
    private readonly ResiliencePipeline<object> _fullPipeline;
    private CircuitState _circuitState = CircuitState.Closed;

    public ResilienceService(ILogger<ResilienceService> logger)
    {
        _logger = logger;

        // Build retry pipeline
        _retryPipeline = BuildRetryPipeline();

        // Build circuit breaker pipeline
        _circuitBreakerPipeline = BuildCircuitBreakerPipeline();

        // Build combined pipeline
        _fullPipeline = BuildFullPipeline();
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _retryPipeline.ExecuteAsync(
                async (ctx) =>
                {
                    var result = await operation();
                    return (object)result!;
                },
                cancellationToken)
                .ConfigureAwait(false) is T typedResult
                ? typedResult
                : throw new InvalidCastException();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {OperationName} failed after retries", operationName);
            throw;
        }
    }

    public async Task<T> ExecuteWithCircuitBreakerAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _circuitBreakerPipeline.ExecuteAsync(
                async (ctx) =>
                {
                    var result = await operation();
                    return (object)result!;
                },
                cancellationToken)
                .ConfigureAwait(false) is T typedResult
                ? typedResult
                : throw new InvalidCastException();
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("Circuit breaker is open for operation {OperationName}", operationName);
            throw new InvalidOperationException(
                $"Service temporarily unavailable for {operationName}. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {OperationName} failed", operationName);
            throw;
        }
    }

    public async Task<T> ExecuteWithFullResilienceAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _fullPipeline.ExecuteAsync(
                async (ctx) =>
                {
                    var result = await operation();
                    return (object)result!;
                },
                cancellationToken)
                .ConfigureAwait(false) is T typedResult
                ? typedResult
                : throw new InvalidCastException();
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning("Circuit breaker is open for operation {OperationName}", operationName);
            throw new InvalidOperationException(
                $"Service temporarily unavailable for {operationName}. Please try again later.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {OperationName} failed after all resilience attempts", operationName);
            throw;
        }
    }

    public CircuitState GetCircuitState() => _circuitState;

    private ResiliencePipeline<object> BuildRetryPipeline()
    {
        return new ResiliencePipelineBuilder<object>()
            .AddRetry(new RetryStrategyOptions<object>
            {
                MaxRetryAttempts = Constants.Retry.MaxAttempts,
                Delay = Constants.Retry.InitialDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = Constants.Retry.MaxDelay,
                ShouldHandle = new PredicateBuilder<object>()
                    .Handle<ApiRequestException>(ex =>
                    {
                        // Retry on rate limits and temporary errors
                        return ex.Message.Contains("Too Many Requests") ||
                               ex.Message.Contains("Bad Gateway") ||
                               ex.Message.Contains("Service Unavailable") ||
                               ex.ErrorCode == 429 ||
                               ex.ErrorCode == 502 ||
                               ex.ErrorCode == 503;
                    })
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry attempt {AttemptNumber} for operation after {Delay}ms. Error: {Error}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private ResiliencePipeline<object> BuildCircuitBreakerPipeline()
    {
        return new ResiliencePipelineBuilder<object>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<object>
            {
                FailureRatio = 0.5,
                MinimumThroughput = Constants.Retry.CircuitBreakerThreshold,
                SamplingDuration = TimeSpan.FromMinutes(1),
                BreakDuration = Constants.Retry.CircuitBreakerDuration,
                ShouldHandle = new PredicateBuilder<object>()
                    .Handle<ApiRequestException>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                OnOpened = args =>
                {
                    _circuitState = CircuitState.Open;
                    _logger.LogError(
                        "Circuit breaker opened after {FailureCount} failures",
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _circuitState = CircuitState.Closed;
                    _logger.LogInformation("Circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _circuitState = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit breaker half-opened, testing service");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private ResiliencePipeline<object> BuildFullPipeline()
    {
        return new ResiliencePipelineBuilder<object>()
            .AddRetry(new RetryStrategyOptions<object>
            {
                MaxRetryAttempts = Constants.Retry.MaxAttempts,
                Delay = Constants.Retry.InitialDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = Constants.Retry.MaxDelay,
                ShouldHandle = new PredicateBuilder<object>()
                    .Handle<ApiRequestException>(ex => ex.ErrorCode == 429 || ex.ErrorCode >= 500)
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {AttemptNumber}/{MaxAttempts} after {Delay}ms",
                        args.AttemptNumber,
                        Constants.Retry.MaxAttempts,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<object>
            {
                FailureRatio = 0.5,
                MinimumThroughput = Constants.Retry.CircuitBreakerThreshold,
                SamplingDuration = TimeSpan.FromMinutes(1),
                BreakDuration = Constants.Retry.CircuitBreakerDuration,
                ShouldHandle = new PredicateBuilder<object>()
                    .Handle<ApiRequestException>()
                    .Handle<HttpRequestException>(),
                OnOpened = args =>
                {
                    _circuitState = CircuitState.Open;
                    _logger.LogError("Circuit breaker OPENED - service unavailable");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _circuitState = CircuitState.Closed;
                    _logger.LogInformation("Circuit breaker CLOSED - service restored");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

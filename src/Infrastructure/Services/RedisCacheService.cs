using System.Text.Json;
using AutonomousResearchAgent.Application.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class RedisCacheService(
    IConnectionMultiplexer connection,
    IOptions<CacheOptions> options,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly CacheOptions _options = options.Value;
    private readonly IDatabase _database = connection.GetDatabase();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var value = await _database.StringGetAsync(prefixedKey);

            if (value.IsNullOrEmpty)
            {
                logger.LogDebug("Cache miss for key {Key}", key);
                return null;
            }

            logger.LogDebug("Cache hit for key {Key}", key);
            return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis connection error getting cache key {Key}", key);
            return null;
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout getting cache key {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cache key {Key}", key);
            return null;
        }
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);
            var expiry = ttl ?? _options.DefaultTtl;

            var result = await _database.StringSetAsync(prefixedKey, serialized, expiry);
            logger.LogDebug("Set cache key {Key} with TTL {Ttl}, result: {Result}", key, expiry, result);
            return result;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis connection error setting cache key {Key}", key);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout setting cache key {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting cache key {Key}", key);
            return false;
        }
    }

    public async Task<bool> SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var expiry = ttl ?? _options.DefaultTtl;

            var result = await _database.StringSetAsync(prefixedKey, value, expiry);
            logger.LogDebug("Set cache key {Key} with TTL {Ttl}, result: {Result}", key, expiry, result);
            return result;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis connection error setting cache key {Key}", key);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout setting cache key {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting cache key {Key}", key);
            return false;
        }
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            var result = await _database.KeyDeleteAsync(prefixedKey);
            logger.LogDebug("Removed cache key {Key}, result: {Result}", key, result);
            return result;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis connection error removing cache key {Key}", key);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout removing cache key {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing cache key {Key}", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            return await _database.KeyExistsAsync(prefixedKey);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis connection error checking existence of cache key {Key}", key);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout checking existence of cache key {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking existence of cache key {Key}", key);
            return false;
        }
    }

    public async Task<bool> SetExpirationAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefixedKey = GetPrefixedKey(key);
            return await _database.KeyExpireAsync(prefixedKey, ttl);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis connection error setting expiration for cache key {Key}", key);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            logger.LogWarning(ex, "Redis timeout setting expiration for cache key {Key}", key);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting expiration for cache key {Key}", key);
            return false;
        }
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var value = await factory();
        if (value != null)
        {
            await SetAsync(key, value, ttl, cancellationToken);
        }

        return value;
    }

    private string GetPrefixedKey(string key) => $"{_options.KeyPrefix}{key}";
}

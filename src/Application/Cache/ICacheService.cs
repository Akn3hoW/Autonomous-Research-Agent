namespace AutonomousResearchAgent.Application.Cache;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;
    Task<bool> SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> SetExpirationAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;
}

public sealed record CacheOptions
{
    public string ConnectionString { get; init; } = "localhost:6379";
    public TimeSpan DefaultTtl { get; init; } = TimeSpan.FromMinutes(5);
    public string KeyPrefix { get; init; } = "ara:";
}

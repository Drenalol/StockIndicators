using LazyCache;
using Microsoft.Extensions.Caching.Memory;

namespace StockIndicators.Providers.Tests.Mocks;

/// <summary>Process-local IAppCache (LazyCache CachingService storage is global and leaks between tests).</summary>
public sealed class FakeAppCache : IAppCache
{
  private readonly Dictionary<string, object?> _store = new();

  public ICacheProvider CacheProvider => throw new NotSupportedException();

  public CacheDefaults DefaultCachePolicy => new();

  public void Add<T>(string key, T value, MemoryCacheEntryOptions options) => _store[key] = value;

  public T? Get<T>(string key) => _store.TryGetValue(key, out var value) ? (T?)value : default;

  public Task<T> GetAsync<T>(string key) => Task.FromResult(Get<T>(key)!);

  public T GetOrAdd<T>(string key, Func<ICacheEntry, T> factory) => throw new NotSupportedException();

  public T GetOrAdd<T>(string key, Func<ICacheEntry, T> factory, MemoryCacheEntryOptions options)
  {
    if (_store.TryGetValue(key, out var existing) && existing is T typed)
      return typed;

    var value = factory(null!);
    _store[key] = value;
    return value;
  }

  public Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> factory) => throw new NotSupportedException();

  public async Task<T> GetOrAddAsync<T>(string key, Func<ICacheEntry, Task<T>> factory, MemoryCacheEntryOptions options)
  {
    if (_store.TryGetValue(key, out var existing) && existing is T typed)
      return typed;

    var value = await factory(null!);
    _store[key] = value;
    return value;
  }

  public void Remove(string key) => _store.Remove(key);

  public bool TryGetValue<T>(string key, out T value)
  {
    if (_store.TryGetValue(key, out var raw) && raw is T typed)
    {
      value = typed;
      return true;
    }

    value = default!;
    return false;
  }
}

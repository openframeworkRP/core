using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace OpenFramework.Api.Services;

public sealed class CacheService
{
    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public CacheService(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var data = await _cache.GetAsync(key);
        return data is null ? null : JsonSerializer.Deserialize<T>(data, JsonOpts);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);
        await _cache.SetAsync(key, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });
    }

    public Task RemoveAsync(string key) => _cache.RemoveAsync(key);

    public Task RemoveManyAsync(params string[] keys)
        => Task.WhenAll(Array.ConvertAll(keys, k => _cache.RemoveAsync(k)));

    // ── Fabriques de clés ───────────────────────────────────────────
    public static string CharKey(string id)            => $"char:{id}";
    public static string CharsOwnerKey(string steamId) => $"chars:owner:{steamId}";
    public static string SelectedKey(string steamId)   => $"char:selected:{steamId}";
    public static string InvKey(string characterId)    => $"inv:{characterId}";
    public static string BankCharKey(string charId)    => $"bank:char:{charId}";
    public static string BankAcctKey(string accountId) => $"bank:acct:{accountId}";
}

using StackExchange.Redis;
using System.Collections.Concurrent;

namespace Valuator;

public interface IRegionDBProvider
{
    IDatabase GetDatabase(string region);
}

public class RegionDBProvider : IRegionDBProvider
{
    private readonly ConcurrentDictionary<string, IConnectionMultiplexer> _connections = new();

    public RegionDBProvider()
    {
        _connections["RU"] = ConnectionMultiplexer.Connect(
            Environment.GetEnvironmentVariable("DB_RU"));

        _connections["EU"] = ConnectionMultiplexer.Connect(
            Environment.GetEnvironmentVariable("DB_EU"));

        _connections["ASIA"] = ConnectionMultiplexer.Connect(
            Environment.GetEnvironmentVariable("DB_ASIA"));
    }

    public IDatabase GetDatabase(string region)
    {
        var key = region.ToUpper();

        if (_connections.TryGetValue(key, out var connection))
            return connection.GetDatabase();

        throw new Exception($"Region {region} not found");
    }
}
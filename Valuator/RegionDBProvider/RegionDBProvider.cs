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
        string redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

        var regionConfigs = new Dictionary<string, string>
        {
            { "RU", "DB_RU" },
            { "EU", "DB_EU" },
            { "ASIA", "DB_ASIA" }
        };

        foreach (var config in regionConfigs)
        {
            string host = Environment.GetEnvironmentVariable(config.Value);

            if (!string.IsNullOrEmpty(host))
            {
                var options = ConfigurationOptions.Parse(host);
                options.Password = redisPassword;
                _connections[config.Key] = ConnectionMultiplexer.Connect(options);
            }
        }
    }

    public IDatabase GetDatabase(string region)
    {
        var key = region.ToUpper();

        if (_connections.TryGetValue(key, out var connection))
            return connection.GetDatabase();

        throw new Exception($"Region {region} not found");
    }
}
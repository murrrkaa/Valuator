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
        var regionConfigs = new Dictionary<string, (string HostVar, string PasswordVar)>
        {
            { "RU",   ("DB_RU",   "REDIS_PASSWORD_RU") },
            { "EU",   ("DB_EU",   "REDIS_PASSWORD_EU") },
            { "ASIA", ("DB_ASIA", "REDIS_PASSWORD_ASIA") }
        };

        foreach (var config in regionConfigs)
        {
            string? host = Environment.GetEnvironmentVariable(config.Value.HostVar);
            string? password = Environment.GetEnvironmentVariable(config.Value.PasswordVar);

            if (!string.IsNullOrEmpty(host))
            {
                var options = ConfigurationOptions.Parse(host);
                options.Password = password;
                options.AbortOnConnectFail = false;

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
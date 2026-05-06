using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

class Program
{
    private const string QueueName = "valuator.processing.rank";
    private const string ExchangeNameEvents = "valuator.processing.events";

    static async Task Main(string[] args)
    {
        Console.Title = "RANK_CALCULATOR";
        var (dbMain, regionDbs) = InitializeDatabases();
        var connection = await CreateRabbitConnection();

        using var channel = await connection.CreateChannelAsync();

        await DeclareTopologyAsync(channel);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var id = Encoding.UTF8.GetString(body);

                string? region = await dbMain.StringGetAsync(id);
                Console.WriteLine($"LOOKUP: {id}, {region}");

                if (string.IsNullOrEmpty(region))
                {
                    Console.WriteLine($"[Error] Region not found for ID {id}");
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                var dbRegion = regionDbs[region];

                var textRedis = await dbRegion.StringGetAsync("TEXT-" + id);

                if (!textRedis.IsNull)
                {
                    string text = textRedis.ToString();

                    TimeSpan interval = TimeSpan.FromSeconds(new Random().Next(3, 10));
                    Console.WriteLine($"Waiting {interval}");
                    await Task.Delay(interval);

                    int nonLetterCount = text.Count(c => !char.IsLetter(c));
                    double rank = (double)nonLetterCount / text.Length;

                    await dbRegion.StringSetAsync("RANK-" + id, rank.ToString());

                    var eventData = new {
                        Id = id,
                        Value = rank,
                        Message = $"[RankCalculated] ID: {id}, Value: {rank}"
                    };

                    var json = JsonSerializer.Serialize(eventData);
                    var bodyMessage = Encoding.UTF8.GetBytes(json);

                    await channel.BasicPublishAsync(
                        exchange: ExchangeNameEvents,
                        routingKey: "",
                        body: bodyMessage
                    );
                }
                else
                {
                    Console.WriteLine($"Текст для ID {id} не найден в базе.");
                }

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Произошла ошибка: {e.Message}");
            }
        };

        await channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer);
        await Task.Delay(Timeout.Infinite);
    }

    private static (IDatabase main, Dictionary<string, IDatabase> regions) InitializeDatabases()
    {
        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");

        ConfigurationOptions GetOptions(string envVar) => new ConfigurationOptions
        {
            EndPoints = { Environment.GetEnvironmentVariable(envVar) ?? "localhost" },
            Password = redisPassword,
            AbortOnConnectFail = false
        };

        var main = ConnectionMultiplexer.Connect(GetOptions("DB_MAIN")).GetDatabase();
        var regions = new Dictionary<string, IDatabase>
        {
            ["RU"] = ConnectionMultiplexer.Connect(GetOptions("DB_RU")).GetDatabase(),
            ["EU"] = ConnectionMultiplexer.Connect(GetOptions("DB_EU")).GetDatabase(),
            ["ASIA"] = ConnectionMultiplexer.Connect(GetOptions("DB_ASIA")).GetDatabase(),
        };

        return (main, regions);
    }

    private static async Task<IConnection> CreateRabbitConnection()
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            UserName = Environment.GetEnvironmentVariable("RABBIT_USER") ?? "",
            Password = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? ""
        };
        return await factory.CreateConnectionAsync();
    }

    private static async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeNameEvents,
            type: ExchangeType.Fanout
        );

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
    }
}
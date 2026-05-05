using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

class Program
{
    private const string QueueName = "valuator.processing.rank";
    private const string ExchangeNameEvents = "valuator.processing.events";
    private const string ExchangeNameNotification = "valuator.processing.notification";

    static async Task Main(string[] args)
    {
        Console.Title = "RANK_CALCULATOR";
        var mainAddr = Environment.GetEnvironmentVariable("DB_MAIN");
        var redis = ConnectionMultiplexer.Connect(mainAddr);
        var dbMain = redis.GetDatabase();

        var regionDbs = new Dictionary<string, IDatabase>
        {
            ["RU"] = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_RU")).GetDatabase(),
            ["EU"] = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_EU")).GetDatabase(),
            ["ASIA"] = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("DB_ASIA")).GetDatabase(),
        };

        var factory = new ConnectionFactory { HostName = "localhost" };

        using var connection = await factory.CreateConnectionAsync();
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

                string region = await dbMain.StringGetAsync(id);
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

                    string rankEvent = $"[RankCalculated] ID: {id}, Value: {rank}";
                    var rankBody = Encoding.UTF8.GetBytes(rankEvent);

                    await channel.BasicPublishAsync(
                        exchange: ExchangeNameEvents,
                        routingKey: "",
                        body: rankBody
                    );

                    var notifyBody = Encoding.UTF8.GetBytes(id);
                    await channel.BasicPublishAsync(exchange: ExchangeNameNotification, routingKey: "", body: notifyBody);
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

        await channel.ExchangeDeclareAsync(exchange: ExchangeNameNotification, type: ExchangeType.Fanout);
    }
}
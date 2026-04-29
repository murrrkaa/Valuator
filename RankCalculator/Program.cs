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

        var redis = ConnectionMultiplexer.Connect("localhost:6379");
        var db = redis.GetDatabase();

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

                var textRedis = await db.StringGetAsync("TEXT-" + id);

                if (!textRedis.IsNull)
                {
                    string text = textRedis.ToString();

                    TimeSpan interval = TimeSpan.FromSeconds(new Random().Next(3, 15));
                    Console.WriteLine($"Waiting {interval}");
                    await Task.Delay(interval);

                    int nonLetterCount = text.Count(c => !char.IsLetter(c));
                    double rank = (double)nonLetterCount / text.Length;

                    await db.StringSetAsync("RANK-" + id, rank.ToString());

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
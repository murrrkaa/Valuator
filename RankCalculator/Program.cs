using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

class Program
{

    private const string QueueName = "valuator.processing.rank";

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

                    int nonLetterCount = text.Count(c => !char.IsLetter(c));
                    double rank = (double)nonLetterCount / text.Length;

                    string setKey = "TEXTS_SET";

                    bool isNew = await db.SetAddAsync(setKey, text);
                    int similarity = isNew ? 0 : 1;

                    await db.StringSetAsync("RANK-" + id, rank.ToString());
                    await db.StringSetAsync("SIMILARITY-" + id, similarity.ToString());
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
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
    }
}
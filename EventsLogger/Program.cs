using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventsLogger;

class Program
{
    private const string ExchangeNameEvents = "valuator.processing.events";

    static async Task Main(string[] args)
    {
        Console.Title = "EVENTS_LOGGER";

        string rabbitUser = Environment.GetEnvironmentVariable("RABBIT_USER");
        string rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD");

        var factory = new ConnectionFactory { HostName = "localhost",
            UserName = rabbitUser,
            Password = rabbitPassword
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        string queueName = await DeclareTopologyAsync(channel);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine(message);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [ERROR]: {ex.Message}");
            }
        };

        await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

        Console.ReadLine();
    }

    private static async Task<string> DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeNameEvents,
            type: ExchangeType.Fanout
        );

        var queueDeclareResult = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true
        );

        string queueName = queueDeclareResult.QueueName;

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: ExchangeNameEvents,
            routingKey: ""
        );

        return queueName;
    }
}
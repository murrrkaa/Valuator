using System.Text;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace Valuator;

public class RankNotifier : BackgroundService
{
    private const string ExchangeNameNotification = "valuator.processing.notification";
    private readonly IHubContext<RankHub> _hub;
    private readonly IConnectionFactory _factory;
    private readonly IConnectionMultiplexer _redis;

    public RankNotifier(IHubContext<RankHub> hub, IConnectionFactory factory, IConnectionMultiplexer redis)
    {
        _hub = hub;
        _factory = factory;
        _redis = redis;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchange: ExchangeNameNotification, type: ExchangeType.Fanout);
        var q = await channel.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queue: q.QueueName, exchange: ExchangeNameNotification, routingKey: "");

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var id = Encoding.UTF8.GetString(ea.Body.ToArray());
            var db = _redis.GetDatabase();
            var rank = await db.StringGetAsync("RANK-" + id);
            var similarity = await db.StringGetAsync("SIMILARITY-" + id);

            await _hub.Clients.Group(id).SendAsync("Receive", id, rank.ToString(), similarity.ToString(), cancellationToken: stoppingToken);
            await channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await channel.BasicConsumeAsync(q.QueueName, autoAck: false, consumer: consumer);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace Valuator;

public class RankNotifier : BackgroundService
{
    private const string ExchangeNameEvents = "valuator.processing.events";
    private readonly IHubContext<RankHub> _hub;
    private readonly IConnection _rabbitConnection;
    private readonly IConnectionMultiplexer _redis;
    private readonly IRegionDBProvider _regionProvider;

    public RankNotifier(IHubContext<RankHub> hub, IConnection rabbitConnection, IConnectionMultiplexer redis, IRegionDBProvider regionDBProvider)
    {
        _hub = hub;
        _rabbitConnection = rabbitConnection;
        _redis = redis;
        _regionProvider = regionDBProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _rabbitConnection.CreateChannelAsync();

        var queue = await channel.QueueDeclareAsync(
            "", 
            durable: false, 
            exclusive: true, 
            autoDelete: true
        );
        await channel.QueueBindAsync(
            queue: queue.QueueName, 
            exchange: ExchangeNameEvents, 
            routingKey: ""
        );

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var jsonString = Encoding.UTF8.GetString(body);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            string id = root.GetProperty("Id").GetString() ?? "";
            string rank = root.GetProperty("Value").GetDouble().ToString();
           
            var dbMain = _redis.GetDatabase();
            string? region = await dbMain.StringGetAsync(id);
            Console.WriteLine($"LOOKUP: {id}, {region}");

            if (string.IsNullOrEmpty(region))
            {
                Console.WriteLine($"[Error] Region not found for ID {id}");
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            var dbRegion = _regionProvider.GetDatabase(region);
            var similarity = await dbRegion.StringGetAsync("SIMILARITY-" + id);
            await _hub.Clients.Group(id).SendAsync("Receive", id, rank.ToString(), similarity.ToString(), cancellationToken: stoppingToken);
            
            await channel.BasicAckAsync(ea.DeliveryTag, false);
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: false, consumer: consumer);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

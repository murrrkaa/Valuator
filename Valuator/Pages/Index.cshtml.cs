using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private const string ExchangeNameRank = "valuator.processing.rank";
    private const string ExchangeNameEvents = "valuator.processing.events";
    private const string QueueName = "valuator.processing.rank";
    private const string RoutingKey = "valuator.processing.rank";

    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConnectionFactory _rabbitFactory;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis, IConnectionFactory rabbitFactory)
    {
        _logger = logger;
        _redis = redis;
        _rabbitFactory = rabbitFactory;
    }

    public void OnGet()
    {

    }

    public async Task<IActionResult> OnPost(string text)
    {
        _logger.LogDebug(text);

        if (string.IsNullOrEmpty(text))
        {
            return RedirectToPage("Index");
        }

        string id = Guid.NewGuid().ToString();
        var db = _redis.GetDatabase();

        string setKey = "TEXTS_SET";

        bool isNew = await db.SetAddAsync(setKey, text);
        int similarity = isNew ? 0 : 1;

        await db.StringSetAsync("SIMILARITY-" + id, similarity.ToString());

        string textKey = "TEXT-" + id;
        await db.StringSetAsync(textKey, text);

        using var connection = await _rabbitFactory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await DeclareTopologyAsync(channel);

        string eventMessage = $"[SimilarityCalculated] ID: {id}, Value: {similarity}";
        var eventBody = Encoding.UTF8.GetBytes(eventMessage);

        await channel.BasicPublishAsync(
            exchange: ExchangeNameEvents,
            routingKey: "",
            body: eventBody
        );

        var body = Encoding.UTF8.GetBytes(id);

        await channel.BasicPublishAsync(
            exchange: ExchangeNameRank,
            routingKey: RoutingKey,
            body: body
         );

        return Redirect($"summary?id={id}");
    }

    private async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(
           exchange: ExchangeNameRank,
           type: ExchangeType.Direct
        );

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

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeNameRank,
            routingKey: RoutingKey
        );
    }
}

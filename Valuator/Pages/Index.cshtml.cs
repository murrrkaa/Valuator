using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private const string ExchangeName = "valuator.processing.rank";
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

        string textKey = "TEXT-" + id;
        db.StringSet(textKey, text);

        using var connection = await _rabbitFactory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await DeclareTopologyAsync(channel);

        var body = Encoding.UTF8.GetBytes(id);

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: RoutingKey,
            body: body
         );

        return Redirect($"summary?id={id}");
    }

    private async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(
           exchange: ExchangeName,
           type: ExchangeType.Direct
        );

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: RoutingKey
        );
    }
}

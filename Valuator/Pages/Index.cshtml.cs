using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
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

        using (var connection = await _rabbitFactory.CreateConnectionAsync())
        using (var channel = await connection.CreateChannelAsync())
        {
            await channel.QueueDeclareAsync(queue: "rank_tasks", durable: false, exclusive: false, autoDelete: false);
            var body = Encoding.UTF8.GetBytes(id);
            await channel.BasicPublishAsync(string.Empty, "rank_tasks", body);
        }

        return Redirect($"summary?id={id}");
    }
}

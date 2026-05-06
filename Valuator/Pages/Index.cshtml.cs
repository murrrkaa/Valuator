using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Channels;
using System.Text.Json;

namespace Valuator.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private const string ExchangeNameRank = "valuator.processing.rank";
    private const string ExchangeNameEvents = "valuator.processing.events";
    private const string QueueName = "valuator.processing.rank";
    private const string RoutingKey = "valuator.processing.rank";

    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IConnection _rabbitConnection;
    private readonly IRegionDBProvider _regionProvider;

    public IndexModel(
    ILogger<IndexModel> logger,
    IConnectionMultiplexer redis,
    IConnection rabbitConnection,
    IRegionDBProvider regionProvider)
    {
        _logger = logger;
        _redis = redis;
        _rabbitConnection = rabbitConnection;
        _regionProvider = regionProvider;
    }

    public void OnGet()
    {

    }

    public async Task<IActionResult> OnPost(string text, string country)
    {
        _logger.LogDebug(text);
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(country))
        {
            return RedirectToPage("Index");
        }
        string id = Guid.NewGuid().ToString();
        string region = GetRegion(country);
        string? author = User?.Identity?.Name ?? "";

        var dbMain = _redis.GetDatabase();
        await dbMain.StringSetAsync("AUTHOR-" + id, author);
        await dbMain.StringSetAsync(id, region);
        var dbRegion = _regionProvider.GetDatabase(region);

        string setKey = "TEXTS_SET";

        bool isNew = await dbRegion.SetAddAsync(setKey, text);
        int similarity = isNew ? 0 : 1;

        await dbRegion.StringSetAsync("SIMILARITY-" + id, similarity.ToString());

        string textKey = "TEXT-" + id;
        await dbRegion.StringSetAsync(textKey, text);

        await PublishProcessingEvents(id, similarity);

        return Redirect($"summary?id={id}");
    }

    private async Task PublishProcessingEvents(string id, int similarity)
    {
        using var channel = await _rabbitConnection.CreateChannelAsync();

        var similarityEvent = new
        {
            Id = id,
            Value = (double)similarity,
            Message = $"[SimilarityCalculated] ID: {id}, Value: {similarity}"
        };

        var eventJson = JsonSerializer.Serialize(similarityEvent);
        var eventBody = Encoding.UTF8.GetBytes(eventJson);

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
    }

    private static string GetRegion(string country) => country switch
    {
        "Russia" => "RU",
        "France" => "EU",
        "Germany" => "EU",
        "UAE" => "ASIA",
        "India" => "ASIA",
        _ => throw new ArgumentException($"Unknown country: {country}")
    };
}

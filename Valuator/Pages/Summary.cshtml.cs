using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using static System.Net.Mime.MediaTypeNames;

namespace Valuator.Pages;
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IRegionDBProvider _regionProvider;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis, IRegionDBProvider regionProvider)
    {
        _logger = logger;
        _redis = redis;
        _regionProvider = regionProvider;
    }

    public double? Rank { get; set; }
    public double? Similarity { get; set; }
    public string? StatusMessage { get; set; }
    public string? TextId { get; set; }

    public async Task OnGet(string id)
    {
        TextId = id;
        _logger.LogDebug(id);

        var dbMain = _redis.GetDatabase();
        string region = await dbMain.StringGetAsync(id);
        Console.WriteLine($"LOOKUP: {id}, {region}");

        if (string.IsNullOrEmpty(region))
        {
            StatusMessage = "Запись не найдена";
            return;
        }
        var dbRegion = _regionProvider.GetDatabase(region);

        var rankValue = await dbRegion.StringGetAsync("RANK-" + id);
        var similarityValue = await dbRegion.StringGetAsync("SIMILARITY-" + id);

        if (rankValue.IsNull)
        {
            StatusMessage = "Оценка содержания не завершена";
            Rank = null;
            Similarity = null;
        }
        else
        {
            Rank = Convert.ToDouble(rankValue.ToString().Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
            Similarity = (double)similarityValue;
        }
    }
}

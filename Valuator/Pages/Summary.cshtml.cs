using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Valuator.Pages;
public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IConnectionMultiplexer _redis;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    public double? Rank { get; set; }
    public double? Similarity { get; set; }
    public string? StatusMessage { get; set; }

    public void OnGet(string id)
    {
        _logger.LogDebug(id);

        var db = _redis.GetDatabase();

        var rankValue = db.StringGet("RANK-" + id);
        var similarityValue = db.StringGet("SIMILARITY-" + id);

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

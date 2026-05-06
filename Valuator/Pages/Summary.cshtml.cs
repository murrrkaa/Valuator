using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using static System.Net.Mime.MediaTypeNames;

namespace Valuator.Pages;

[Authorize]
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

    public async Task<IActionResult> OnGet(string id)
    {
        TextId = id;
        _logger.LogDebug(id);

        if (!await IsUserAuthorized(id))
        {
            return Forbid();
        }

        await LoadSummaryData(id);

        return Page();
    }

    private async Task<bool> IsUserAuthorized(string id)
    {
        var dbMain = _redis.GetDatabase();
        string author = await dbMain.StringGetAsync("AUTHOR-" + id);

        return !string.IsNullOrEmpty(author) && author == User.Identity.Name;
    }

    private async Task LoadSummaryData(string id)
    {
        var dbMain = _redis.GetDatabase();
        string region = await dbMain.StringGetAsync(id);

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
        }
        else
        {
            Rank = Convert.ToDouble(rankValue.ToString().Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
            Similarity = (double)similarityValue;
        }
    }
}

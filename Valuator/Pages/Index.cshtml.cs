using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConnectionMultiplexer _redis;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis;
    }

    public void OnGet()
    {

    }

    public IActionResult OnPost(string text)
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
        // TODO: (pa1) сохранить в БД (Redis) text по ключу textKey

        int nonLetterCount = text.Count(c => !char.IsLetter(c));
        double rank = (double)nonLetterCount / text.Length;

        string rankKey = "RANK-" + id;
        db.StringSet(rankKey, rank);
        // TODO: (pa1) посчитать rank и сохранить в БД (Redis) по ключу rankKey

        string similarityKey = "SIMILARITY-" + id;
        string setKey = "TEXTS_SET";

        bool isNew = db.SetAdd(setKey, text);
        int similarity = isNew ? 0 : 1;

        db.StringSet(similarityKey, similarity);
        // TODO: (pa1) посчитать similarity и сохранить в БД (Redis) по ключу similarityKey

        return Redirect($"summary?id={id}");
    }
}

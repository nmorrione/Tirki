using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Tirki.Models;

namespace Tirki.Services;

/// <summary>
/// Suggerisce una categoria per un nuovo movimento guardando le proprie transazioni già
/// categorizzate: se la descrizione digitata condivide parole con quelle di transazioni passate,
/// propone la categoria più usata tra i risultati simili. Nessuna chiamata di rete: euristica
/// locale su parole chiave, pensata per lo storico personale (poche centinaia/migliaia di righe).
/// </summary>
public class CategorySuggestionService
{
    private static readonly HashSet<string> Stopwords = new()
    {
        "di", "da", "in", "con", "su", "per", "tra", "fra", "il", "lo", "la", "i", "gli", "le",
        "un", "uno", "una", "e", "o", "del", "della", "dei", "delle", "al", "allo", "alla",
        "ai", "agli", "alle", "the", "and",
    };

    private static readonly Regex TokenSplitRegex = new(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled);

    private readonly LocalDatabaseService _database;

    public CategorySuggestionService(LocalDatabaseService database)
    {
        _database = database;
    }

    public async Task<Guid?> SuggestCategoryAsync(string description)
    {
        var inputTokens = Tokenize(description);
        if (inputTokens.Count == 0) return null;

        var transactions = await _database.GetTransactionsAsync();

        var scores = new Dictionary<Guid, int>();
        foreach (var transaction in transactions)
        {
            if (transaction.CategoryId is not { } categoryId) continue;

            var historyTokens = Tokenize(transaction.Description);
            if (historyTokens.Count == 0) continue;

            var overlap = inputTokens.Count(historyTokens.Contains);
            if (overlap == 0) continue;

            scores[categoryId] = scores.GetValueOrDefault(categoryId) + overlap;
        }

        if (scores.Count == 0) return null;

        return scores.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    private static HashSet<string> Tokenize(string text)
    {
        var normalized = RemoveDiacritics(text.ToLowerInvariant());
        return TokenSplitRegex.Split(normalized)
            .Where(token => token.Length >= 3 && !Stopwords.Contains(token))
            .ToHashSet();
    }

    private static string RemoveDiacritics(string text)
    {
        var formD = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

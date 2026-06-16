using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hermes.Models;

namespace Hermes.Services;

public interface IMarketDataService
{
    Task<IReadOnlyCollection<MarketCompany>> SearchCompaniesAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MarketInstrument>> SearchInstrumentsAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CurrencyItem>> GetCurrencyItemsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CurrencyValue>> GetCurrencyValuesAsync(IReadOnlyCollection<int> currencyIds, CancellationToken cancellationToken = default);
    Task<MarketQuote?> GetQuoteAsync(int companyId, CancellationToken cancellationToken = default);
}

public sealed class NadpcoMarketDataService(HttpClient httpClient) : IMarketDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private const string StaticBearerToken = "E0EE30A995F3C6ED48A038275D81BFEF724A2B0C688E90E2485C0D8B1700BD487B4CC68648F9C280ACA8106BCE2C2CCE8382DC8AA3EA834E6A3747C532BE4C7A";

    public async Task<IReadOnlyCollection<MarketCompany>> SearchCompaniesAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "/api/v3/BaseInfo/Companies", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<List<NadpcoCompany>>(stream, JsonOptions, cancellationToken) ?? [];
        var normalizedQuery = (query ?? "").Trim();

        var companies = payload
            .Where(item => item.PrecedencyRight == 0)
            .Select(item => new MarketCompany(
                item.CoID,
                item.CoSymbol ?? item.BourseSymbol ?? "",
                item.CoTitle ?? item.FullTitle ?? "",
                item.CoSymbolEnglish,
                item.MarketTitle,
                item.IndustryTitle,
                item.FundTypeID is not null || (item.FundTypeTitle ?? "").Contains("صندوق", StringComparison.OrdinalIgnoreCase)))
            .Where(item => !string.IsNullOrWhiteSpace(item.BourseSymbol) && !string.IsNullOrWhiteSpace(item.FullTitle))
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery)
                || item.BourseSymbol.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || item.FullTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || (item.SymbolEnglish?.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(item => item.IsFund)
            .ThenBy(item => item.BourseSymbol)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();

        return companies;
    }

    public async Task<IReadOnlyCollection<CurrencyItem>> GetCurrencyItemsAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "/api/v2/Currency/items", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<List<CurrencyItem>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyCollection<MarketInstrument>> SearchInstrumentsAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = (query ?? "").Trim();
        var companyLimit = Math.Clamp(limit, 1, 100);
        var companies = (await SearchCompaniesAsync(normalizedQuery, companyLimit, cancellationToken)).Select(item => new MarketInstrument(
            "company",
            item.CoId,
            item.BourseSymbol,
            item.FullTitle,
            item.IsFund ? "ETF" : "Tehran Stock",
            null,
            null,
            item.MarketTitle ?? item.IndustryTitle));

        var currencies = (await GetCurrencyItemsAsync(cancellationToken))
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery)
                || item.CurrencySymbol.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || item.CurrencyTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(item => new MarketInstrument(
                "currency",
                item.CurrencyId,
                item.CurrencySymbol,
                item.CurrencyTitle,
                "Currency",
                null,
                0m,
                "Priced in Rial"));

        return companies
            .Concat(currencies)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    public async Task<IReadOnlyCollection<CurrencyValue>> GetCurrencyValuesAsync(IReadOnlyCollection<int> currencyIds, CancellationToken cancellationToken = default)
    {
        var ids = currencyIds.Count == 0 ? Array.Empty<int>() : currencyIds.Distinct().ToArray();
        using var request = await CreateRequestAsync(HttpMethod.Post, "/api/v2/currency/values/rt?IsTimePeriod=false", cancellationToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { currencyIds = ids }, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var values = await ParseCurrencyValuesAsync(stream, cancellationToken);
        return ids.Length == 0 ? values : values.Where(item => ids.Contains(item.CurrencyId)).ToList();
    }

    public async Task<MarketQuote?> GetQuoteAsync(int companyId, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"/api/v3/TS/RealTimeTradesToday?companyid={companyId}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var item = await ParseQuoteAsync(stream, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new MarketQuote(
            item.CoID,
            item.BourseSymbol ?? "",
            item.FullTitle ?? "",
            item.TradeDateGre,
            item.TradeDate,
            item.MaxPrice,
            item.MinPrice,
            item.OpeningPrice,
            item.ClosingPrice,
            item.LastPrice,
            item.PreviousClosingPrice,
            item.ClosingPriceChange,
            item.ClosingPChgPercent,
            item.TradeVolume,
            item.TradeValue,
            item.MarketValue);
    }

    private static Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", StaticBearerToken);
        return Task.FromResult(request);
    }

    private static async Task<IReadOnlyCollection<CurrencyValue>> ParseCurrencyValuesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "data", "items", "values", "result" })
            {
                if (root.TryGetProperty(name, out var child) && child.ValueKind == JsonValueKind.Array)
                {
                    root = child;
                    break;
                }
            }
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return root.EnumerateArray().Select(item =>
        {
            var values = item.TryGetProperty("currencyValues", out var nested) && nested.ValueKind == JsonValueKind.Object ? nested : item;
            return new CurrencyValue(
                GetInt(item, "currencyId"),
                GetString(item, "currencySymbol"),
                GetString(item, "currencyTitle") is { Length: > 0 } title ? title : GetString(item, "currencyName"),
                GetDecimal(values, "currencyCloseValue") ?? GetDecimal(values, "closeValue") ?? GetDecimal(values, "value"),
                GetDate(values, "dateTime") ?? GetDate(values, "dateGre") ?? GetDate(values, "updatedAt") ?? GetDate(values, "tradeDateGre"));
        }).ToList();
    }

    private static async Task<NadpcoQuote?> ParseQuoteAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var first = document.RootElement.EnumerateArray().FirstOrDefault();
            return first.ValueKind == JsonValueKind.Undefined
                ? null
                : first.Deserialize<NadpcoQuote>(JsonOptions);
        }

        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.Deserialize<NadpcoQuote>(JsonOptions)
            : null;
    }

    private static string GetString(JsonElement item, string name)
        => item.TryGetProperty(name, out var property) ? property.GetString() ?? "" : "";

    private static int GetInt(JsonElement item, string name)
        => item.TryGetProperty(name, out var property) && property.TryGetInt32(out var value) ? value : 0;

    private static decimal? GetDecimal(JsonElement item, string name)
        => item.TryGetProperty(name, out var property) && property.TryGetDecimal(out var value) ? value : null;

    private static DateTimeOffset? GetDate(JsonElement item, string name)
        => item.TryGetProperty(name, out var property) && property.TryGetDateTimeOffset(out var value) ? value : null;

    private sealed record NadpcoCompany(
        int CoID,
        string? BourseSymbol,
        string? FullTitle,
        string? CoTitle,
        string? CoSymbol,
        string? CoSymbolEnglish,
        string? MarketTitle,
        string? IndustryTitle,
        int? PrecedencyRight,
        int? FundTypeID,
        string? FundTypeTitle);

    private sealed record NadpcoQuote(
        int CoID,
        string? BourseSymbol,
        string? FullTitle,
        DateTime? TradeDateGre,
        string? TradeDate,
        decimal? MaxPrice,
        decimal? MinPrice,
        decimal? OpeningPrice,
        decimal? ClosingPrice,
        decimal? LastPrice,
        decimal? PreviousClosingPrice,
        decimal? ClosingPriceChange,
        decimal? ClosingPChgPercent,
        decimal? TradeVolume,
        decimal? TradeValue,
        decimal? MarketValue);
}

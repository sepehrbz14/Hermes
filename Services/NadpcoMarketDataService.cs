using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hermes.Models;

namespace Hermes.Services;

public sealed record NadpcoDebugEntry(string Method, string Url, string ResponseBody, DateTimeOffset Timestamp);

public sealed class NadpcoDebugLog
{
    private const int MaxEntries = 20;
    private readonly System.Collections.Concurrent.ConcurrentQueue<NadpcoDebugEntry> _entries = new();

    public void Record(string method, string url, string responseBody)
    {
        if (!url.Contains("/api/v2/Token", StringComparison.OrdinalIgnoreCase)
            && !url.Contains("/api/v2/currency/values/rt", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _entries.Enqueue(new NadpcoDebugEntry(method, url, responseBody, DateTimeOffset.UtcNow));
        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyCollection<NadpcoDebugEntry> Drain()
    {
        var entries = new List<NadpcoDebugEntry>();
        while (_entries.TryDequeue(out var entry))
        {
            entries.Add(entry);
        }

        return entries;
    }
}

public interface IMarketDataService
{
    Task<IReadOnlyCollection<MarketCompany>> SearchCompaniesAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MarketInstrument>> SearchInstrumentsAsync(string? query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CurrencyItem>> GetCurrencyItemsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CurrencyValue>> GetCurrencyValuesAsync(IReadOnlyCollection<int> currencyIds, CancellationToken cancellationToken = default);
    Task<MarketInstrument?> GetCryptoQuoteAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<MarketQuote?> GetQuoteAsync(int companyId, CancellationToken cancellationToken = default);
}

public sealed partial class NadpcoMarketDataService(HttpClient httpClient, IWebHostEnvironment environment, IConfiguration configuration, NadpcoDebugLog debugLog) : IMarketDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private const string StaticBearerToken = "E0EE30A995F3C6ED48A038275D81BFEF724A2B0C688E90E2485C0D8B1700BD487B4CC68648F9C280ACA8106BCE2C2CCE8382DC8AA3EA834E6A3747C532BE4C7A";
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _bearerToken = configuration["Nadpco:BearerToken"] ?? StaticBearerToken;

    private static readonly string[] CryptoSymbols =
    [
        "BTCIRT",
        "ETHIRT",
        "LTCIRT",
        "IRT",
        "XRPIRT",
        "BCHIRT",
        "BNBIRT",
        "EOSIRT",
        "XLMIRT",
        "ETCIRT",
        "TRXIRT",
        "DOGEIRT",
        "UNIIRT",
        "DAIIRT",
        "LINKIRT",
        "DOTIRT",
        "AAVEIRT",
        "ADAIRT",
        "SHIBIRT",
        "FTMIRT",
        "MATICIRT",
        "AXSIRT",
        "MANAIRT",
        "SANDIRT",
        "AVAXIRT",
        "MKRIRT",
        "GMTIRT",
        "USDCIRT",
        "CHZIRT",
        "GRTIRT",
        "CRVIRT",
        "EGLDIRT",
        "GALIRT",
        "HBARIRT",
        "IMXIRT",
        "WBTCIRT",
        "ONEIRT",
        "ENSIRT",
        "1M_BTTIRT",
        "SUSHIIRT",
        "LDOIRT",
        "ZROIRT",
        "STORJIRT",
        "ANTIRT",
        "100K_FLOKIIRT",
        "GLMIRT",
        "XMRIRT",
        "OMIRT",
        "RDNTIRT",
        "TIRT",
        "ATOMIRT",
        "NOTIRT",
        "CVXIRT",
        "XTZIRT",
        "FILIRT",
        "UMAIRT",
        "1B_BABYDOGEIRT",
        "BANDIRT",
        "SSVIRT",
        "DAOIRT",
        "BLURIRT",
        "GMXIRT",
        "WIRT",
        "SKLIRT",
        "SNTIRT",
        "NMRIRT",
        "API3IRT",
        "CVCIRT",
        "WLDIRT",
        "SOLIRT",
        "AEVOIRT",
        "QNTIRT",
        "FETIRT",
        "AGIXIRT",
        "LPTIRT",
        "SLPIRT",
        "COMPIRT",
        "MEMEIRT",
        "BATIRT",
        "SNXIRT",
        "TRBIRT",
        "RSRIRT",
        "RNDRIRT",
        "YFIIRT",
        "MDTIRT",
        "LRCIRT",
        "1M_PEPEIRT",
        "BICOIRT",
        "MAGICIRT",
        "ETHFIIRT",
        "1INCHIRT",
        "1M_NFTIRT",
        "ARBIRT",
        "DYDXIRT",
        "BALIRT",
        "TONIRT",
        "APTIRT",
        "CELRIRT",
        "ALGOIRT",
        "NEARIRT",
        "ZRXIRT",
        "MASKIRT",
        "EGALAIRT",
        "FLOWIRT",
        "OMGIRT",
        "APEIRT",
        "WOOIRT",
        "ENJIRT",
        "JSTIRT"
    ];

    public async Task<IReadOnlyCollection<MarketCompany>> SearchCompaniesAsync(string? query, int limit, CancellationToken cancellationToken = default)
    {
        var payload = await GetStaticCompaniesAsync(cancellationToken);
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

    public Task<IReadOnlyCollection<CurrencyItem>> GetCurrencyItemsAsync(CancellationToken cancellationToken = default)
        => GetStaticCurrenciesAsync(cancellationToken);

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

        var cryptos = CryptoSymbols
            .Select((symbol, index) => new { symbol, sourceId = index + 1, asset = GetCryptoAssetSymbol(symbol) })
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery)
                || item.symbol.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || item.asset.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(item => new MarketInstrument(
                "crypto",
                item.sourceId,
                item.symbol,
                $"{item.asset} / IRT",
                "Crypto",
                null,
                0m,
                "Nobitex crypto market"));

        return companies
            .Concat(currencies)
            .Concat(cryptos)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    public async Task<IReadOnlyCollection<CurrencyValue>> GetCurrencyValuesAsync(IReadOnlyCollection<int> currencyIds, CancellationToken cancellationToken = default)
    {
        var ids = currencyIds.Count == 0 ? Array.Empty<int>() : currencyIds.Distinct().ToArray();
        var body = JsonSerializer.Serialize(new { currencyIds = ids }, JsonOptions);
        var text = await SendNadpcoAsync(
            HttpMethod.Post,
            "/api/v2/currency/values/rt?IsTimePeriod=false",
            () => new StringContent(body, Encoding.UTF8, "application/json"),
            cancellationToken);

        await using var stream = ToStream(text);
        var values = await ParseCurrencyValuesAsync(stream, cancellationToken);
        return ids.Length == 0 ? values : values.Where(item => ids.Contains(item.CurrencyId)).ToList();
    }

    public async Task<MarketInstrument?> GetCryptoQuoteAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        if (sourceId <= 0 || sourceId > CryptoSymbols.Length)
        {
            return null;
        }

        var symbol = CryptoSymbols[sourceId - 1];
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://apiv2.nobitex.ir/v3/orderbook/{symbol}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var orderBook = await JsonSerializer.DeserializeAsync<NobitexOrderBook>(stream, JsonOptions, cancellationToken);
        if (!string.Equals(orderBook?.Status, "ok", StringComparison.OrdinalIgnoreCase) || orderBook.LastTradePrice is null)
        {
            return null;
        }

        var asset = GetCryptoAssetSymbol(symbol);
        return new MarketInstrument(
            "crypto",
            sourceId,
            symbol,
            $"{asset} / IRT",
            "Crypto",
            orderBook.LastTradePrice,
            0m,
            orderBook.LastUpdate is null ? "Nobitex last trade" : $"Nobitex last trade • {DateTimeOffset.FromUnixTimeMilliseconds(orderBook.LastUpdate.Value):u}");
    }

    public async Task<MarketQuote?> GetQuoteAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var text = await SendNadpcoAsync(
            HttpMethod.Get,
            $"/api/v3/TS/RealTimeTradesToday?companyid={companyId}",
            null,
            cancellationToken);

        await using var stream = ToStream(text);
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


    private async Task<IReadOnlyCollection<NadpcoCompany>> GetStaticCompaniesAsync(CancellationToken cancellationToken)
    {
        var path = GetStaticJsonPath("Companies.json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return DeserializeStaticCompanies(json);
    }

    private async Task<IReadOnlyCollection<CurrencyItem>> GetStaticCurrenciesAsync(CancellationToken cancellationToken)
    {
        var path = GetStaticJsonPath("Currencies.json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return DeserializeStaticCurrencies(json);
    }

    private string GetStaticJsonPath(string fileName)
    {
        var contentRootPath = Path.Combine(environment.ContentRootPath, "JSONS", fileName);
        return File.Exists(contentRootPath)
            ? contentRootPath
            : Path.Combine(AppContext.BaseDirectory, "JSONS", fileName);
    }

    private static IReadOnlyCollection<NadpcoCompany> DeserializeStaticCompanies(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<NadpcoCompany>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            var coIdIndex = json.IndexOf("\\\"coID", StringComparison.Ordinal);
            if (coIdIndex < 0)
            {
                throw;
            }

            var escapedPayload = json[(coIdIndex - 1)..];
            var normalized = Regex.Unescape(Regex.Unescape(escapedPayload));
            var arrayEnd = normalized.LastIndexOf(']');
            if (arrayEnd >= 0)
            {
                normalized = normalized[..(arrayEnd + 1)];
            }

            return JsonSerializer.Deserialize<List<NadpcoCompany>>("[{" + normalized, JsonOptions) ?? [];
        }
    }

    private static IReadOnlyCollection<CurrencyItem> DeserializeStaticCurrencies(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<CurrencyItem>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return CurrencyItemRegex().Matches(json)
                .Select(match => new CurrencyItem(
                    int.Parse(match.Groups["id"].Value),
                    match.Groups["symbol"].Value,
                    match.Groups["title"].Value))
                .ToList();
        }
    }

    private async Task<string> SendNadpcoAsync(HttpMethod method, string path, Func<HttpContent>? createContent, CancellationToken cancellationToken)
    {
        var responseText = await SendNadpcoOnceAsync(method, path, createContent, await GetBearerTokenAsync(false, cancellationToken), cancellationToken);
        if (!IsExpiredToken(responseText))
        {
            return responseText;
        }

        responseText = await SendNadpcoOnceAsync(method, path, createContent, await GetBearerTokenAsync(true, cancellationToken), cancellationToken);
        if (IsExpiredToken(responseText))
        {
            throw new InvalidOperationException("NADPCO token refresh succeeded, but the refreshed token is still rejected as expired.");
        }

        return responseText;
    }

    private async Task<string> SendNadpcoOnceAsync(HttpMethod method, string path, Func<HttpContent>? createContent, string bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = createContent?.Invoke();

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        debugLog.Record(method.Method, request.RequestUri?.ToString() ?? path, responseText);
        if (!response.IsSuccessStatusCode && !IsExpiredToken(responseText))
        {
            response.EnsureSuccessStatusCode();
        }

        return responseText;
    }

    private async Task<string> GetBearerTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && !string.IsNullOrWhiteSpace(_bearerToken))
        {
            return _bearerToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && !string.IsNullOrWhiteSpace(_bearerToken))
            {
                return _bearerToken;
            }

            _bearerToken = await RequestBearerTokenAsync(cancellationToken);
            return _bearerToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private async Task<string> RequestBearerTokenAsync(CancellationToken cancellationToken)
    {
        var username = configuration["Nadpco:Username"] ?? "IOS153183309";
        var password = configuration["Nadpco:Password"] ?? "OcSQYanxgRNTBIJ";
        var query = $"username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";
        var attempts = new (string Path, Func<HttpContent?> CreateContent)[]
        {
            ("/api/v2/Token", () => new StringContent(JsonSerializer.Serialize(new { username, password }, JsonOptions), Encoding.UTF8, "application/json")),
            ("/api/v2/Token", () => new StringContent(JsonSerializer.Serialize(new { userName = username, password }, JsonOptions), Encoding.UTF8, "application/json")),
            ("/api/v2/Token", () => new StringContent(JsonSerializer.Serialize(new { UserName = username, Password = password }, JsonOptions), Encoding.UTF8, "application/json")),
            ("/api/v2/Token", () => new StringContent(JsonSerializer.Serialize(new { Username = username, Password = password }, JsonOptions), Encoding.UTF8, "application/json")),
            ("/api/v2/Token", () => new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password
            })),
            ("/api/v2/Token", () => new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = username,
                ["password"] = password
            })),
            ($"/api/v2/Token?{query}", () => null)
        };

        string? lastResponseText = null;
        foreach (var (path, createContent) in attempts)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = createContent();

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            debugLog.Record("POST", request.RequestUri?.ToString() ?? "/api/v2/Token", responseText);
            lastResponseText = responseText;

            var token = NormalizeBearerToken(ExtractToken(responseText));
            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        throw new InvalidOperationException($"NADPCO token response did not include a bearer token. Last response: {TrimForError(lastResponseText)}");
    }

    private static string? ExtractToken(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.ValueKind != JsonValueKind.String)
            {
                return FindToken(document.RootElement);
            }

            var token = document.RootElement.GetString();
            if (token?.TrimStart().StartsWith('{') == true)
            {
                return ExtractToken(token);
            }

            return token;
        }
        catch (JsonException)
        {
            var token = responseText.Trim().Trim('"');
            return LooksLikeToken(token) ? token : null;
        }
    }

    private static string? NormalizeBearerToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        token = token.Trim().Trim('"');
        const string bearerPrefix = "Bearer ";
        return token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..].Trim()
            : token;
    }

    private static string? FindToken(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (property.Name.Equals("token", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("accessToken", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("bearerToken", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("access_token", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Equals("jwt", StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }

                    if ((property.Name.Equals("data", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Equals("result", StringComparison.OrdinalIgnoreCase)
                            || property.Name.Equals("value", StringComparison.OrdinalIgnoreCase))
                        && LooksLikeToken(value))
                    {
                        return value;
                    }
                }

                var nested = FindToken(property.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindToken(item);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return element.ValueKind == JsonValueKind.String && LooksLikeToken(element.GetString())
            ? element.GetString()
            : null;
    }

    private static bool LooksLikeToken(string? value)
    {
        value = NormalizeBearerToken(value);
        return value is { Length: >= 32 }
            && !value.Contains(' ', StringComparison.Ordinal)
            && !value.Contains('{', StringComparison.Ordinal)
            && !value.Contains('}', StringComparison.Ordinal);
    }

    private static string TrimForError(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return "<empty>";
        }

        responseText = responseText.Trim();
        return responseText.Length <= 500 ? responseText : responseText[..500];
    }

    private static bool IsExpiredToken(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            return (root.TryGetProperty("errorCode", out var code) && code.TryGetInt32(out var errorCode) && errorCode == 1008)
                || (root.TryGetProperty("errorType", out var type) && string.Equals(type.GetString(), "ExpiredToken", StringComparison.OrdinalIgnoreCase))
                || (root.TryGetProperty("additionalData", out var additionalData)
                    && additionalData.ValueKind == JsonValueKind.String
                    && additionalData.GetString()?.Contains("Expired Token", StringComparison.OrdinalIgnoreCase) == true);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static MemoryStream ToStream(string text) => new(Encoding.UTF8.GetBytes(text));

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

    private static string GetCryptoAssetSymbol(string symbol)
        => symbol.EndsWith("IRT", StringComparison.OrdinalIgnoreCase) && symbol.Length > 3
            ? symbol[..^3]
            : symbol;

    [GeneratedRegex("\\\"currencyId\\\"\\s*:\\s*(?<id>\\d+).*?\\\"currencySymbol\\\"\\s*:\\s*\\\"(?<symbol>[^\\\"\\r\\n]+)\\\".*?\\\"currencyTitle\\\"\\s*:\\s*\\\"(?<title>[^\\\"\\r\\n]+)\\\"", RegexOptions.Singleline)]
    private static partial Regex CurrencyItemRegex();

    private sealed record NobitexOrderBook(
        string Status,
        long? LastUpdate,
        decimal? LastTradePrice);

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

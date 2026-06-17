using System.Text;
using System.Text.Json;
using Hermes.Models;
using Hermes.Services;

var builder = WebApplication.CreateBuilder(args);
var projectWebRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var outputWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
builder.WebHost.UseWebRoot(Directory.Exists(projectWebRoot) ? projectWebRoot : outputWebRoot);

builder.Services.AddDataProtection();
builder.Services.AddSingleton<PortfolioStore>();
builder.Services.AddSingleton<IBrokerageProvider, PlaceholderBrokerageProvider>();
builder.Services.AddSingleton<NadpcoDebugLog>();
builder.Services.AddHttpClient<IMarketDataService, NadpcoMarketDataService>((services, client) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(configuration["Nadpco:BaseUrl"] ?? "https://data3.nadpco.com");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
var indexPath = Path.Combine(app.Environment.WebRootPath ?? outputWebRoot, "index.html");

app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var debugLog = context.RequestServices.GetRequiredService<NadpcoDebugLog>();
    context.Response.OnStarting(() =>
    {
        var entries = debugLog.Drain();
        if (entries.Count > 0)
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            context.Response.Headers["X-Nadpco-Debug"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        return Task.CompletedTask;
    });

    await next();
});

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    application = "Hermes",
    marketData = "nadpco"
}));

app.MapPost("/api/auth/login", (LoginRequest request, PortfolioStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new ApiError("Email and password are required."));
    }

    if (request.Password.Length < 4)
    {
        return Results.BadRequest(new ApiError("Password must be at least 4 characters."));
    }

    var user = store.SignIn(request.Email, request.Password, request.RememberMe);
    return Results.Ok(user);
});

app.MapPost("/api/auth/signup", (SignupRequest request, PortfolioStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new ApiError("Name and email are required."));
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new ApiError("Password must be at least 8 characters."));
    }

    if (request.Password != request.ConfirmPassword)
    {
        return Results.BadRequest(new ApiError("Passwords need to match."));
    }

    var user = store.CreateUser(request.Name, request.Email, request.Phone, request.Password);
    return Results.Ok(user);
});

app.MapPost("/api/auth/google/start", (GoogleAuthStartRequest request, PortfolioStore store) =>
{
    var result = store.StartGoogleSignIn(request.Mode);
    return result.Configured ? Results.Ok(result) : Results.BadRequest(new ApiError(result.Message));
});

app.MapPost("/api/auth/logout", () => Results.NoContent());

app.MapGet("/api/portfolio", (PortfolioStore store) => Results.Ok(store.GetSnapshot()));

app.MapPost("/api/portfolio/holdings", (HoldingRequest request, PortfolioStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Ticker))
    {
        return Results.BadRequest(new ApiError("Asset name and ticker are required."));
    }

    if (request.Shares <= 0 || request.Price <= 0)
    {
        return Results.BadRequest(new ApiError("Shares and price must be greater than zero."));
    }

    var holding = store.AddHolding(request);
    return Results.Created($"/api/portfolio/holdings/{holding.Id}", holding);
});

app.MapPut("/api/portfolio/holdings/{id:guid}", (Guid id, HoldingRequest request, PortfolioStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Ticker))
    {
        return Results.BadRequest(new ApiError("Asset name and ticker are required."));
    }

    if (request.Shares <= 0 || request.Price <= 0)
    {
        return Results.BadRequest(new ApiError("Shares and price must be greater than zero."));
    }

    var holding = store.UpdateHolding(id, request);
    return holding is null
        ? Results.NotFound(new ApiError("Holding was not found."))
        : Results.Ok(holding);
});

app.MapDelete("/api/portfolio/holdings/{id:guid}", (Guid id, PortfolioStore store) =>
{
    return store.RemoveHolding(id)
        ? Results.NoContent()
        : Results.NotFound(new ApiError("Holding was not found."));
});

app.MapPost("/api/portfolio/watchlist", (WatchlistRequest request, PortfolioStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Source) || request.SourceId <= 0 || string.IsNullOrWhiteSpace(request.Symbol))
    {
        return Results.BadRequest(new ApiError("A market instrument is required."));
    }

    return Results.Ok(store.AddWatchItem(request));
});

app.MapDelete("/api/portfolio/watchlist/{source}/{sourceId:int}", (string source, int sourceId, PortfolioStore store) =>
{
    return store.RemoveWatchItem(source, sourceId)
        ? Results.NoContent()
        : Results.NotFound(new ApiError("Watchlist item was not found."));
});

app.MapPost("/api/portfolio/display-name", (DisplayNameRequest request, PortfolioStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new ApiError("Display name is required."));
    }

    return Results.Ok(store.UpdateDisplayName(request.Name));
});

app.MapGet("/api/brokerages/status", (IBrokerageProvider provider) => Results.Ok(provider.GetStatus()));

app.MapGet("/api/brokerages/accounts", async (IBrokerageProvider provider) =>
{
    var accounts = await provider.GetAccountsAsync();
    return Results.Ok(accounts);
});

app.MapPost("/api/brokerages/sync", async (IBrokerageProvider provider, PortfolioStore store) =>
{
    var result = await provider.SyncAsync();
    store.RecordActivity(result.Message);
    return Results.Ok(result);
});

app.MapGet("/api/market/companies", async (string? query, int? limit, IMarketDataService marketData, CancellationToken cancellationToken) =>
{
    try
    {
        var companies = await marketData.SearchCompaniesAsync(query, limit ?? 50, cancellationToken);
        return Results.Ok(companies);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not load NADPCO companies: {ex.Message}");
    }
});

app.MapGet("/api/market/instruments", async (string? query, int? limit, IMarketDataService marketData, CancellationToken cancellationToken) =>
{
    try
    {
        var instruments = await marketData.SearchInstrumentsAsync(query, limit ?? 50, cancellationToken);
        return Results.Ok(instruments);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not load NADPCO instruments: {ex.Message}");
    }
});

app.MapGet("/api/market/instruments/{source}/{sourceId:int}/quote", async (string source, int sourceId, IMarketDataService marketData, CancellationToken cancellationToken) =>
{
    try
    {
        if (source.Equals("company", StringComparison.OrdinalIgnoreCase))
        {
            var quote = await marketData.GetQuoteAsync(sourceId, cancellationToken);
            return quote is null
                ? Results.Ok(new MarketInstrument("company", sourceId, "Market Close", "Market Close", "Tehran Stock", null, null, "Market Close"))
                : Results.Ok(new MarketInstrument("company", sourceId, quote.BourseSymbol, quote.FullTitle, "Tehran Stock", quote.ClosingPrice ?? quote.LastPrice, quote.ClosingPChgPercent, quote.TradeDate));
        }

        if (source.Equals("currency", StringComparison.OrdinalIgnoreCase))
        {
            var currencies = await marketData.GetCurrencyItemsAsync(cancellationToken);
            var currency = currencies.FirstOrDefault(item => item.CurrencyId == sourceId);
            var value = (await marketData.GetCurrencyValuesAsync([sourceId], cancellationToken)).FirstOrDefault();
            return currency is null
                ? Results.NotFound(new ApiError("Currency was not found."))
                : Results.Ok(new MarketInstrument("currency", sourceId, currency.CurrencySymbol, currency.CurrencyTitle, "Currency", value?.CurrencyCloseValue, 0m, value?.UpdatedAt?.ToString("u") ?? "Priced in Rial"));
        }

        if (source.Equals("crypto", StringComparison.OrdinalIgnoreCase))
        {
            var quote = await marketData.GetCryptoQuoteAsync(sourceId, cancellationToken);
            return quote is null
                ? Results.NotFound(new ApiError("Crypto instrument was not found."))
                : Results.Ok(quote);
        }

        return Results.BadRequest(new ApiError("Unknown market instrument source."));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not load market instrument quote: {ex.Message}");
    }
});

app.MapGet("/api/market/companies/{companyId:int}/quote", async (int companyId, IMarketDataService marketData, CancellationToken cancellationToken) =>
{
    try
    {
        var quote = await marketData.GetQuoteAsync(companyId, cancellationToken);
        return quote is null
            ? Results.Ok(new MarketInstrument("company", companyId, "Market Close", "Market Close", "Tehran Stock", null, null, "Market Close"))
            : Results.Ok(quote);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not load NADPCO quote: {ex.Message}");
    }
});

app.MapGet("/api/market/currencies", async (IMarketDataService marketData, CancellationToken cancellationToken) =>
{
    try
    {
        var currencies = await marketData.GetCurrencyItemsAsync(cancellationToken);
        return Results.Ok(currencies);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not load NADPCO currencies: {ex.Message}");
    }
});

app.MapPost("/api/market/currencies/values", async (CurrencyValueRequest request, IMarketDataService marketData, CancellationToken cancellationToken) =>
{
    try
    {
        var values = await marketData.GetCurrencyValuesAsync(request.CurrencyIds, cancellationToken);
        return Results.Ok(values);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Could not load NADPCO currency values: {ex.Message}");
    }
});

app.MapGet("/", () => File.Exists(indexPath)
    ? Results.File(indexPath, "text/html")
    : Results.NotFound(new ApiError("wwwroot/index.html was not found. Build or run the app from the project root.")));

app.MapFallback(() => File.Exists(indexPath)
    ? Results.File(indexPath, "text/html")
    : Results.NotFound(new ApiError("wwwroot/index.html was not found. Build or run the app from the project root.")));

app.Run();

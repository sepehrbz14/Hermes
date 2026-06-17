namespace Hermes.Models;

public sealed record ApiError(string Message);

public sealed record UserProfile(
    Guid Id,
    string Name,
    string Email,
    bool RememberMe,
    string AuthProvider);

public sealed record Holding(
    Guid Id,
    int? CompanyId,
    int? CurrencyId,
    string Source,
    int? SourceId,
    string Name,
    string Ticker,
    string Type,
    decimal Shares,
    decimal Price,
    decimal CurrentPrice,
    decimal Change,
    DateOnly PurchaseDate);

public sealed record WatchItem(
    string Source,
    int SourceId,
    string Ticker,
    string Name,
    string Type,
    decimal Price,
    decimal Change);

public sealed record PortfolioSummary(
    decimal TotalValue,
    decimal DailyMove,
    decimal DailyPercent,
    decimal TotalGainPercent,
    int RiskScore);

public sealed record PortfolioSnapshot(
    UserProfile? User,
    IReadOnlyCollection<Holding> Holdings,
    IReadOnlyCollection<WatchItem> Watchlist,
    IReadOnlyCollection<string> Activity,
    PortfolioSummary Summary);

public sealed record LoginRequest(string Email, string Password, bool RememberMe);

public sealed record SignupRequest(
    string Name,
    string Email,
    string Phone,
    string Password,
    string ConfirmPassword);

public sealed record HoldingRequest(
    string Name,
    string Ticker,
    string Type,
    decimal Shares,
    decimal Price,
    decimal? CurrentPrice,
    string? Source,
    int? SourceId,
    int? CompanyId,
    int? CurrencyId,
    DateOnly? PurchaseDate);

public sealed record DisplayNameRequest(string Name);

public sealed record BrokerageStatus(
    string Mode,
    bool Connected,
    string Message,
    IReadOnlyCollection<string> PendingIntegrations);

public sealed record BrokerageAccount(
    string Brokerage,
    string AccountName,
    string AccountType,
    decimal MarketValue,
    string Status);

public sealed record BrokerageSyncResult(
    bool Success,
    string Message,
    DateTimeOffset SyncedAt);

public sealed record MarketCompany(
    int CoId,
    string BourseSymbol,
    string FullTitle,
    string? SymbolEnglish,
    string? MarketTitle,
    string? IndustryTitle,
    bool IsFund);

public sealed record MarketQuote(
    int CoId,
    string BourseSymbol,
    string FullTitle,
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

public sealed record CurrencyItem(
    int CurrencyId,
    string CurrencySymbol,
    string CurrencyTitle);

public sealed record CurrencyValue(
    int CurrencyId,
    string CurrencySymbol,
    string CurrencyTitle,
    decimal? CurrencyCloseValue,
    DateTimeOffset? UpdatedAt);

public sealed record CurrencyValueRequest(IReadOnlyCollection<int> CurrencyIds);

public sealed record MarketInstrument(
    string Source,
    int SourceId,
    string Symbol,
    string Title,
    string Type,
    decimal? Price,
    decimal? Change,
    string? Subtitle);

public sealed record WatchlistRequest(
    string Source,
    int SourceId,
    string Symbol,
    string Title,
    string Type,
    decimal Price,
    decimal Change);

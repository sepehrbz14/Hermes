using Hermes.Models;

namespace Hermes.Services;

public interface IBrokerageProvider
{
    BrokerageStatus GetStatus();
    Task<IReadOnlyCollection<BrokerageAccount>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<BrokerageSyncResult> SyncAsync(CancellationToken cancellationToken = default);
}

public sealed class PlaceholderBrokerageProvider : IBrokerageProvider
{
    public BrokerageStatus GetStatus() => new(
        "nadpco",
        true,
        "NADPCO market-data endpoints are configured for Tehran stocks, ETFs, and currencies. Brokerage account APIs can still be added here later.",
        ["Tehran Stock Exchange companies", "ETF base info", "Real-time trades today", "Currency items", "Currency close values"]);

    public Task<IReadOnlyCollection<BrokerageAccount>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<BrokerageAccount> accounts =
        [
            new("NADPCO", "Market data feed", "Tehran exchange data", 0m, "Configured")
        ];

        return Task.FromResult(accounts);
    }

    public Task<BrokerageSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var result = new BrokerageSyncResult(
            false,
            "NADPCO market-data connection is configured. Full brokerage account sync can be wired here when brokerage APIs are available.",
            DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }
}

using Hermes.Models;

namespace Hermes.Services;

public sealed class PortfolioStore
{
    private readonly object _gate = new();
    private readonly List<Holding> _holdings = [];

    private readonly List<WatchItem> _watchlist = [];

    private readonly List<string> _activity =
    [
        "Portfolio opened for morning review",
        "Dividend forecast updated",
        "Risk score recalculated"
    ];

    private UserProfile? _user;

    public UserProfile SignIn(string email, bool rememberMe)
    {
        var name = MakeDisplayName(email);
        var user = new UserProfile(Guid.NewGuid(), name, email.Trim(), rememberMe, "password");

        lock (_gate)
        {
            _user = user;
            _activity.Insert(0, $"Signed in as {user.Email}");
        }

        return user;
    }

    public UserProfile CreateUser(string name, string email)
    {
        var user = new UserProfile(Guid.NewGuid(), name.Trim(), email.Trim(), true, "password");

        lock (_gate)
        {
            _user = user;
            _activity.Insert(0, $"Created account for {user.Email}");
        }

        return user;
    }

    public UserProfile SignInWithGoogle()
    {
        var user = new UserProfile(Guid.NewGuid(), "Google Investor", "google@hermes.local", true, "google");

        lock (_gate)
        {
            _user = user;
            _activity.Insert(0, "Signed in with Google");
        }

        return user;
    }

    public UserProfile UpdateDisplayName(string name)
    {
        lock (_gate)
        {
            _user ??= new UserProfile(Guid.NewGuid(), "Investor", "investor@hermes.local", false, "local");
            _user = _user with { Name = name.Trim() };
            _activity.Insert(0, $"Updated display name to {_user.Name}");
            return _user;
        }
    }

    public PortfolioSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var holdings = _holdings.ToList();
            return new PortfolioSnapshot(
                _user,
                holdings,
                _watchlist.ToList(),
                _activity.Take(8).ToList(),
                BuildSummary(holdings));
        }
    }

    public Holding AddHolding(HoldingRequest request)
    {
        var holding = new Holding(
            Guid.NewGuid(),
            request.CompanyId,
            request.CurrencyId,
            NormalizeSource(request),
            request.SourceId ?? request.CurrencyId ?? request.CompanyId,
            request.Name.Trim(),
            request.Ticker.Trim(),
            request.Type.Trim(),
            request.Shares,
            request.Price,
            request.CurrentPrice ?? request.Price,
            CalculateChange(request.Price, request.CurrentPrice ?? request.Price),
            request.PurchaseDate ?? DateOnly.FromDateTime(DateTime.Today));

        lock (_gate)
        {
            _holdings.Insert(0, holding);
            _activity.Insert(0, $"Added {holding.Ticker} to holdings");
        }

        return holding;
    }

    public Holding? UpdateHolding(Guid id, HoldingRequest request)
    {
        lock (_gate)
        {
            var index = _holdings.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return null;
            }

            var existing = _holdings[index];
            var currentPrice = request.CurrentPrice ?? existing.CurrentPrice;
            var updated = existing with
            {
                CompanyId = request.CompanyId,
                CurrencyId = request.CurrencyId,
                Source = NormalizeSource(request),
                SourceId = request.SourceId ?? request.CurrencyId ?? request.CompanyId,
                Name = request.Name.Trim(),
                Ticker = request.Ticker.Trim(),
                Type = request.Type.Trim(),
                Shares = request.Shares,
                Price = request.Price,
                CurrentPrice = currentPrice,
                Change = CalculateChange(request.Price, currentPrice),
                PurchaseDate = request.PurchaseDate ?? existing.PurchaseDate
            };

            _holdings[index] = updated;
            _activity.Insert(0, $"Updated {updated.Ticker} holding");
            return updated;
        }
    }

    public bool RemoveHolding(Guid id)
    {
        lock (_gate)
        {
            var holding = _holdings.FirstOrDefault(item => item.Id == id);
            if (holding is null)
            {
                return false;
            }

            _holdings.Remove(holding);
            _activity.Insert(0, $"Removed {holding.Ticker} from holdings");
            return true;
        }
    }

    public WatchItem AddWatchItem(WatchlistRequest request)
    {
        var item = new WatchItem(
            request.Source.Trim(),
            request.SourceId,
            request.Symbol.Trim(),
            request.Title.Trim(),
            request.Type.Trim(),
            request.Price,
            request.Change);

        lock (_gate)
        {
            _watchlist.RemoveAll(existing => existing.Source == item.Source && existing.SourceId == item.SourceId);
            _watchlist.Insert(0, item);
            _activity.Insert(0, $"Added {item.Ticker} to watchlist");
        }

        return item;
    }

    public bool RemoveWatchItem(string source, int sourceId)
    {
        lock (_gate)
        {
            var removed = _watchlist.RemoveAll(item => item.Source == source && item.SourceId == sourceId) > 0;
            if (removed)
            {
                _activity.Insert(0, "Updated watchlist");
            }

            return removed;
        }
    }

    public void RecordActivity(string message)
    {
        lock (_gate)
        {
            _activity.Insert(0, message);
        }
    }

    private static PortfolioSummary BuildSummary(IReadOnlyCollection<Holding> holdings)
    {
        var total = holdings.Sum(HoldingValue);
        var daily = holdings.Sum(asset => HoldingValue(asset) * asset.Change / 100m);
        var dailyPercent = total == 0 ? 0 : daily / total * 100m;
        var riskScore = Math.Min(82, Math.Max(22, (int)Math.Round(30 + holdings.Count * 4 + Math.Abs(dailyPercent) * 8)));
        return new PortfolioSummary(total, daily, dailyPercent, 14.82m, riskScore);
    }

    private static string NormalizeSource(HoldingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            return request.Source.Trim().ToLowerInvariant();
        }

        return request.CurrencyId is not null ? "currency" : "company";
    }

    private static decimal HoldingValue(Holding asset) => asset.Shares * asset.CurrentPrice;

    private static decimal CalculateChange(decimal purchasePrice, decimal currentPrice)
    {
        return purchasePrice == 0 ? 0 : Math.Round((currentPrice - purchasePrice) / purchasePrice * 100m, 2);
    }

    private static string MakeDisplayName(string email)
    {
        var namePart = email.Split('@')[0].Replace(".", " ").Replace("_", " ").Replace("-", " ");
        return string.Join(' ', namePart
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}

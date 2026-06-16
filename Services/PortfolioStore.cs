using Hermes.Models;

namespace Hermes.Services;

public sealed class PortfolioStore
{
    private readonly object _gate = new();
    private readonly List<Holding> _holdings =
    [
        new(Guid.NewGuid(), 3, null, "company", "معدنی و صنعتی چادرملو", "کچاد", "Tehran Stock", 1200m, 2626m, -1.32m, DateOnly.FromDateTime(DateTime.Today)),
        new(Guid.NewGuid(), 1, null, "company", "کشاورزی و دامپروری مگسال", "زمگسا", "Tehran Stock", 840m, 12540m, .64m, DateOnly.FromDateTime(DateTime.Today)),
        new(Guid.NewGuid(), 12345, null, "company", "صندوق سرمایه گذاری عیار", "عیار", "ETF", 500m, 10000m, .21m, DateOnly.FromDateTime(DateTime.Today)),
        new(Guid.NewGuid(), null, 84, "currency", "دلار NerkhApi", "price_usd_nerkhapi", "Currency", 2000m, 1m, 0m, DateOnly.FromDateTime(DateTime.Today)),
        new(Guid.NewGuid(), null, 85, "currency", "یورو", "price_eur", "Currency", 750m, 1m, 0m, DateOnly.FromDateTime(DateTime.Today))
    ];

    private readonly List<WatchItem> _watchlist =
    [
        new("company", 3, "کچاد", "معدنی و صنعتی چادرملو", "Tehran Stock", 2626m, -1.32m),
        new("company", 1, "زمگسا", "کشاورزی و دامپروری مگسال", "Tehran Stock", 12540m, .64m),
        new("company", 12345, "عیار", "صندوق سرمایه گذاری عیار", "ETF", 10000m, .21m),
        new("currency", 84, "price_usd_nerkhapi", "دلار NerkhApi", "Currency", 1m, 0m)
    ];

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
            request.CurrencyId is not null ? "currency" : "company",
            request.Name.Trim(),
            request.Ticker.Trim(),
            request.Type.Trim(),
            request.Shares,
            request.Price,
            request.Type.Equals("Currency", StringComparison.OrdinalIgnoreCase) ? 0m : Math.Round(Random.Shared.Next(-200, 201) / 100m, 2),
            request.PurchaseDate ?? DateOnly.FromDateTime(DateTime.Today));

        lock (_gate)
        {
            _holdings.Insert(0, holding);
            _activity.Insert(0, $"Added {holding.Ticker} to holdings");
        }

        return holding;
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

    private static decimal HoldingValue(Holding asset) => asset.Shares * asset.Price;

    private static string MakeDisplayName(string email)
    {
        var namePart = email.Split('@')[0].Replace(".", " ").Replace("_", " ").Replace("-", " ");
        return string.Join(' ', namePart
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}

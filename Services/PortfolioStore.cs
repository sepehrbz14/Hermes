using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hermes.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Hermes.Services;

public sealed class PortfolioStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int PasswordIterations = 150_000;
    private readonly object _gate = new();
    private readonly string _connectionString;
    private readonly IDataProtector _protector;

    private readonly List<string> _activity =
    [
        "Portfolio opened for morning review",
        "Dividend forecast updated",
        "Risk score recalculated"
    ];

    private UserProfile? _user;

    public PortfolioStore(IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
    {
        _connectionString = configuration.GetConnectionString("HermesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:HermesDb is required.");
        _protector = dataProtectionProvider.CreateProtector("Hermes.PortfolioStore.v1");
        EnsureDatabase();
    }

    public UserProfile SignIn(string email, string password, bool rememberMe)
    {
        var normalizedEmail = NormalizeEmail(email);
        using var connection = OpenConnection();
        using var command = new SqlCommand("""
            SELECT Id, NameProtected, Email, PasswordHash, PasswordSalt, AuthProvider
            FROM dbo.Users
            WHERE NormalizedEmail = @email AND AuthProvider = 'password'
            """, connection);
        command.Parameters.AddWithValue("@email", normalizedEmail);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Email or password is incorrect.");
        }

        var hash = GetString(reader, "PasswordHash");
        var salt = GetString(reader, "PasswordSalt");
        if (!VerifyPassword(password, salt, hash))
        {
            throw new InvalidOperationException("Email or password is incorrect.");
        }

        var user = new UserProfile(
            reader.GetGuid(reader.GetOrdinal("Id")),
            Unprotect(GetString(reader, "NameProtected")),
            GetString(reader, "Email"),
            rememberMe,
            GetString(reader, "AuthProvider"));

        lock (_gate)
        {
            _user = user;
            _activity.Insert(0, $"Signed in as {user.Email}");
        }

        return user;
    }

    public UserProfile CreateUser(string name, string email, string? phone, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        using var connection = OpenConnection();
        if (UserExists(connection, normalizedEmail))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var userId = Guid.NewGuid();
        var salt = CreateSalt();
        var passwordHash = HashPassword(password, salt);
        using var command = new SqlCommand("""
            INSERT INTO dbo.Users (Id, NameProtected, Email, NormalizedEmail, PhoneProtected, PasswordHash, PasswordSalt, AuthProvider, CreatedAtUtc)
            VALUES (@id, @name, @email, @normalizedEmail, @phone, @passwordHash, @passwordSalt, 'password', SYSUTCDATETIME())
            """, connection);
        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@name", Protect(name.Trim()));
        command.Parameters.AddWithValue("@email", email.Trim());
        command.Parameters.AddWithValue("@normalizedEmail", normalizedEmail);
        command.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : Protect(phone.Trim()));
        command.Parameters.AddWithValue("@passwordHash", passwordHash);
        command.Parameters.AddWithValue("@passwordSalt", salt);
        command.ExecuteNonQuery();

        var user = new UserProfile(userId, name.Trim(), email.Trim(), true, "password");
        lock (_gate)
        {
            _user = user;
            _activity.Insert(0, $"Created account for {user.Email}");
        }

        return user;
    }

    public UserProfile SignInWithGoogle(string name, string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        using var connection = OpenConnection();
        using var lookup = new SqlCommand("""
            SELECT Id, NameProtected, Email, AuthProvider
            FROM dbo.Users
            WHERE NormalizedEmail = @email
            """, connection);
        lookup.Parameters.AddWithValue("@email", normalizedEmail);

        using (var reader = lookup.ExecuteReader())
        {
            if (reader.Read())
            {
                var existingUser = new UserProfile(
                    reader.GetGuid(reader.GetOrdinal("Id")),
                    Unprotect(GetString(reader, "NameProtected")),
                    GetString(reader, "Email"),
                    true,
                    GetString(reader, "AuthProvider"));

                lock (_gate)
                {
                    _user = existingUser;
                    _activity.Insert(0, $"Signed in with Google as {existingUser.Email}");
                }

                return existingUser;
            }
        }

        var userId = Guid.NewGuid();
        using var insert = new SqlCommand("""
            INSERT INTO dbo.Users (Id, NameProtected, Email, NormalizedEmail, PhoneProtected, PasswordHash, PasswordSalt, AuthProvider, CreatedAtUtc)
            VALUES (@id, @name, @email, @normalizedEmail, NULL, NULL, NULL, 'google', SYSUTCDATETIME())
            """, connection);
        insert.Parameters.AddWithValue("@id", userId);
        insert.Parameters.AddWithValue("@name", Protect(name.Trim()));
        insert.Parameters.AddWithValue("@email", email.Trim());
        insert.Parameters.AddWithValue("@normalizedEmail", normalizedEmail);
        insert.ExecuteNonQuery();

        var user = new UserProfile(userId, name.Trim(), email.Trim(), true, "google");
        lock (_gate)
        {
            _user = user;
            _activity.Insert(0, $"Created Google account for {user.Email}");
        }

        return user;
    }

    public UserProfile UpdateDisplayName(string name)
    {
        var user = RequireUser();
        using var connection = OpenConnection();
        using var command = new SqlCommand("UPDATE dbo.Users SET NameProtected = @name WHERE Id = @id", connection);
        command.Parameters.AddWithValue("@name", Protect(name.Trim()));
        command.Parameters.AddWithValue("@id", user.Id);
        command.ExecuteNonQuery();

        lock (_gate)
        {
            _user = user with { Name = name.Trim() };
            _activity.Insert(0, $"Updated display name to {_user.Name}");
            return _user;
        }
    }

    public PortfolioSnapshot GetSnapshot()
    {
        var user = _user;
        if (user is null)
        {
            return new PortfolioSnapshot(null, [], [], _activity.Take(8).ToList(), BuildSummary([]));
        }

        var holdings = LoadHoldings(user.Id);
        var watchlist = LoadWatchlist(user.Id);
        return new PortfolioSnapshot(user, holdings, watchlist, _activity.Take(8).ToList(), BuildSummary(holdings));
    }

    public Holding AddHolding(HoldingRequest request)
    {
        var user = RequireUser();
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

        using var connection = OpenConnection();
        using var command = new SqlCommand("""
            INSERT INTO dbo.Holdings (Id, UserId, PayloadProtected, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@id, @userId, @payload, SYSUTCDATETIME(), SYSUTCDATETIME())
            """, connection);
        command.Parameters.AddWithValue("@id", holding.Id);
        command.Parameters.AddWithValue("@userId", user.Id);
        command.Parameters.AddWithValue("@payload", Protect(JsonSerializer.Serialize(holding, JsonOptions)));
        command.ExecuteNonQuery();

        lock (_gate)
        {
            _activity.Insert(0, $"Added {holding.Ticker} to holdings");
        }

        return holding;
    }

    public Holding? UpdateHolding(Guid id, HoldingRequest request)
    {
        var user = RequireUser();
        var existing = LoadHolding(user.Id, id);
        if (existing is null)
        {
            return null;
        }

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

        using var connection = OpenConnection();
        using var command = new SqlCommand("""
            UPDATE dbo.Holdings
            SET PayloadProtected = @payload, UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @id AND UserId = @userId
            """, connection);
        command.Parameters.AddWithValue("@payload", Protect(JsonSerializer.Serialize(updated, JsonOptions)));
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@userId", user.Id);
        command.ExecuteNonQuery();

        lock (_gate)
        {
            _activity.Insert(0, $"Updated {updated.Ticker} holding");
        }

        return updated;
    }

    public bool RemoveHolding(Guid id)
    {
        var user = RequireUser();
        using var connection = OpenConnection();
        using var command = new SqlCommand("DELETE FROM dbo.Holdings WHERE Id = @id AND UserId = @userId", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@userId", user.Id);
        var removed = command.ExecuteNonQuery() > 0;
        if (removed)
        {
            lock (_gate)
            {
                _activity.Insert(0, "Removed holding from portfolio");
            }
        }

        return removed;
    }

    public WatchItem AddWatchItem(WatchlistRequest request)
    {
        var user = RequireUser();
        var item = new WatchItem(
            request.Source.Trim(),
            request.SourceId,
            request.Symbol.Trim(),
            request.Title.Trim(),
            request.Type.Trim(),
            request.Price,
            request.Change);

        using var connection = OpenConnection();
        using var delete = new SqlCommand("DELETE FROM dbo.Watchlist WHERE UserId = @userId AND Source = @source AND SourceId = @sourceId", connection);
        delete.Parameters.AddWithValue("@userId", user.Id);
        delete.Parameters.AddWithValue("@source", item.Source);
        delete.Parameters.AddWithValue("@sourceId", item.SourceId);
        delete.ExecuteNonQuery();

        using var insert = new SqlCommand("""
            INSERT INTO dbo.Watchlist (UserId, Source, SourceId, PayloadProtected, CreatedAtUtc, UpdatedAtUtc)
            VALUES (@userId, @source, @sourceId, @payload, SYSUTCDATETIME(), SYSUTCDATETIME())
            """, connection);
        insert.Parameters.AddWithValue("@userId", user.Id);
        insert.Parameters.AddWithValue("@source", item.Source);
        insert.Parameters.AddWithValue("@sourceId", item.SourceId);
        insert.Parameters.AddWithValue("@payload", Protect(JsonSerializer.Serialize(item, JsonOptions)));
        insert.ExecuteNonQuery();

        lock (_gate)
        {
            _activity.Insert(0, $"Added {item.Ticker} to watchlist");
        }

        return item;
    }

    public bool RemoveWatchItem(string source, int sourceId)
    {
        var user = RequireUser();
        using var connection = OpenConnection();
        using var command = new SqlCommand("DELETE FROM dbo.Watchlist WHERE UserId = @userId AND Source = @source AND SourceId = @sourceId", connection);
        command.Parameters.AddWithValue("@userId", user.Id);
        command.Parameters.AddWithValue("@source", source);
        command.Parameters.AddWithValue("@sourceId", sourceId);
        var removed = command.ExecuteNonQuery() > 0;
        if (removed)
        {
            lock (_gate)
            {
                _activity.Insert(0, "Updated watchlist");
            }
        }

        return removed;
    }

    public void RecordActivity(string message)
    {
        lock (_gate)
        {
            _activity.Insert(0, message);
        }
    }

    private IReadOnlyCollection<Holding> LoadHoldings(Guid userId)
    {
        using var connection = OpenConnection();
        using var command = new SqlCommand("SELECT PayloadProtected FROM dbo.Holdings WHERE UserId = @userId ORDER BY CreatedAtUtc DESC", connection);
        command.Parameters.AddWithValue("@userId", userId);
        using var reader = command.ExecuteReader();
        var holdings = new List<Holding>();
        while (reader.Read())
        {
            var holding = JsonSerializer.Deserialize<Holding>(Unprotect(GetString(reader, "PayloadProtected")), JsonOptions);
            if (holding is not null)
            {
                holdings.Add(holding);
            }
        }

        return holdings;
    }

    private Holding? LoadHolding(Guid userId, Guid id)
    {
        using var connection = OpenConnection();
        using var command = new SqlCommand("SELECT PayloadProtected FROM dbo.Holdings WHERE Id = @id AND UserId = @userId", connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@userId", userId);
        var payload = command.ExecuteScalar() as string;
        return payload is null ? null : JsonSerializer.Deserialize<Holding>(Unprotect(payload), JsonOptions);
    }

    private IReadOnlyCollection<WatchItem> LoadWatchlist(Guid userId)
    {
        using var connection = OpenConnection();
        using var command = new SqlCommand("SELECT PayloadProtected FROM dbo.Watchlist WHERE UserId = @userId ORDER BY CreatedAtUtc DESC", connection);
        command.Parameters.AddWithValue("@userId", userId);
        using var reader = command.ExecuteReader();
        var watchlist = new List<WatchItem>();
        while (reader.Read())
        {
            var item = JsonSerializer.Deserialize<WatchItem>(Unprotect(GetString(reader, "PayloadProtected")), JsonOptions);
            if (item is not null)
            {
                watchlist.Add(item);
            }
        }

        return watchlist;
    }

    private void EnsureDatabase()
    {
        EnsureCatalog();
        using var connection = OpenConnection();
        using var command = new SqlCommand("""
            IF OBJECT_ID('dbo.Users', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                    NameProtected NVARCHAR(MAX) NOT NULL,
                    Email NVARCHAR(320) NOT NULL,
                    NormalizedEmail NVARCHAR(320) NOT NULL CONSTRAINT UQ_Users_NormalizedEmail UNIQUE,
                    PhoneProtected NVARCHAR(MAX) NULL,
                    PasswordHash NVARCHAR(256) NULL,
                    PasswordSalt NVARCHAR(256) NULL,
                    AuthProvider NVARCHAR(32) NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL
                );
            END;

            IF OBJECT_ID('dbo.Holdings', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Holdings (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Holdings PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    PayloadProtected NVARCHAR(MAX) NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL,
                    UpdatedAtUtc DATETIME2 NOT NULL,
                    CONSTRAINT FK_Holdings_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_Holdings_UserId ON dbo.Holdings(UserId);
            END;

            IF OBJECT_ID('dbo.Watchlist', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Watchlist (
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    Source NVARCHAR(32) NOT NULL,
                    SourceId INT NOT NULL,
                    PayloadProtected NVARCHAR(MAX) NOT NULL,
                    CreatedAtUtc DATETIME2 NOT NULL,
                    UpdatedAtUtc DATETIME2 NOT NULL,
                    CONSTRAINT PK_Watchlist PRIMARY KEY (UserId, Source, SourceId),
                    CONSTRAINT FK_Watchlist_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
                );
            END;
            """, connection);
        command.ExecuteNonQuery();
    }

    private static string GetString(IDataRecord record, string name)
        => record.GetString(record.GetOrdinal(name));

    private void EnsureCatalog()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        builder.InitialCatalog = "master";
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        using var command = new SqlCommand($"IF DB_ID(@databaseName) IS NULL CREATE DATABASE {QuoteSqlIdentifier(databaseName)}", connection);
        command.Parameters.AddWithValue("@databaseName", databaseName);
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqlException)
        {
            // If the login cannot create databases, the later app connection will surface the actionable error.
        }
    }

    private static string QuoteSqlIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static bool UserExists(SqlConnection connection, string normalizedEmail)
    {
        using var command = new SqlCommand("SELECT 1 FROM dbo.Users WHERE NormalizedEmail = @email", connection);
        command.Parameters.AddWithValue("@email", normalizedEmail);
        return command.ExecuteScalar() is not null;
    }

    private SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private UserProfile RequireUser()
        => _user ?? throw new InvalidOperationException("Please sign in before managing your portfolio.");

    private string Protect(string value) => _protector.Protect(value);

    private string Unprotect(string value) => _protector.Unprotect(value);

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

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string CreateSalt()
    {
        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);
        return Convert.ToBase64String(salt);
    }

    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string salt, string expectedHash)
    {
        var actualHash = HashPassword(password, salt);
        var actualBytes = Convert.FromBase64String(actualHash);
        var expectedBytes = Convert.FromBase64String(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}

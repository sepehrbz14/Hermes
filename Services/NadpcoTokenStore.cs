using Microsoft.Data.SqlClient;

namespace Hermes.Services;

public sealed class NadpcoTokenStore
{
    private readonly string _connectionString;

    public NadpcoTokenStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HermesDb")
            ?? throw new InvalidOperationException("ConnectionStrings:HermesDb is required.");
        EnsureCatalog();
        EnsureTable();
    }

    public string? GetToken()
    {
        using var connection = OpenConnection();
        using var command = new SqlCommand("SELECT TOP (1) BearerToken FROM dbo.NadpcoTokens WHERE Id = 1", connection);
        return command.ExecuteScalar() as string;
    }

    public void SaveToken(string token)
    {
        using var connection = OpenConnection();
        using var command = new SqlCommand("""
            MERGE dbo.NadpcoTokens AS target
            USING (SELECT 1 AS Id) AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET BearerToken = @token, UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Id, BearerToken, UpdatedAtUtc) VALUES (1, @token, SYSUTCDATETIME());
            """, connection);
        command.Parameters.AddWithValue("@token", token);
        command.ExecuteNonQuery();
    }

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

    private void EnsureTable()
    {
        using var connection = OpenConnection();
        using var command = new SqlCommand("""
            IF OBJECT_ID('dbo.NadpcoTokens', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.NadpcoTokens (
                    Id INT NOT NULL CONSTRAINT PK_NadpcoTokens PRIMARY KEY,
                    BearerToken NVARCHAR(MAX) NOT NULL,
                    UpdatedAtUtc DATETIME2 NOT NULL
                );
            END;
            """, connection);
        command.ExecuteNonQuery();
    }

    private static string QuoteSqlIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}

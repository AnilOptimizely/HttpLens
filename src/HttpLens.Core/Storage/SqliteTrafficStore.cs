using System.Globalization;
using System.Text.Json;
using HttpLens.Core.Configuration;
using HttpLens.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace HttpLens.Core.Storage;

/// <summary>
/// Thread-safe SQLite-backed traffic store with ring-buffer eviction.
/// </summary>
/// <param name="options">HttpLens options.</param>
public sealed class SqliteTrafficStore(IOptions<HttpLensOptions> options) : ITrafficStore
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpLensOptions _options = options.Value;
    private readonly object _sync = new();
    private readonly string _connectionString = BuildConnectionString(options.Value.SqliteDatabasePath);
    private bool _isInitialized;

    /// <inheritdoc />
    public event Action<HttpTrafficRecord>? OnRecordAdded;

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_sync)
            {
                EnsureInitialized();
                using var connection = CreateConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM TrafficRecords;";
                return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }
    }

    /// <inheritdoc />
    public void Add(HttpTrafficRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_sync)
        {
            EnsureInitialized();
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO TrafficRecords (
                    Id, Timestamp, Duration, HttpClientName, RequestMethod, RequestUri,
                    RequestHeaders, RequestBody, RequestContentType, RequestBodySizeBytes,
                    ResponseStatusCode, ResponseHeaders, ResponseBody, ResponseContentType, ResponseBodySizeBytes,
                    IsSuccess, Exception, TraceId, ParentSpanId, InboundRequestPath, AttemptNumber, RetryGroupId
                ) VALUES (
                    @Id, @Timestamp, @Duration, @HttpClientName, @RequestMethod, @RequestUri,
                    @RequestHeaders, @RequestBody, @RequestContentType, @RequestBodySizeBytes,
                    @ResponseStatusCode, @ResponseHeaders, @ResponseBody, @ResponseContentType, @ResponseBodySizeBytes,
                    @IsSuccess, @Exception, @TraceId, @ParentSpanId, @InboundRequestPath, @AttemptNumber, @RetryGroupId
                );
                """;
            command.Parameters.AddWithValue("@Id", record.Id.ToString("D", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@Timestamp", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@Duration", record.Duration.ToString("c", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@HttpClientName", record.HttpClientName);
            command.Parameters.AddWithValue("@RequestMethod", record.RequestMethod);
            command.Parameters.AddWithValue("@RequestUri", record.RequestUri);
            command.Parameters.AddWithValue("@RequestHeaders", SerializeHeaders(record.RequestHeaders));
            command.Parameters.AddWithValue("@RequestBody", (object?)record.RequestBody ?? DBNull.Value);
            command.Parameters.AddWithValue("@RequestContentType", (object?)record.RequestContentType ?? DBNull.Value);
            command.Parameters.AddWithValue("@RequestBodySizeBytes", (object?)record.RequestBodySizeBytes ?? DBNull.Value);
            command.Parameters.AddWithValue("@ResponseStatusCode", (object?)record.ResponseStatusCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@ResponseHeaders", SerializeHeaders(record.ResponseHeaders));
            command.Parameters.AddWithValue("@ResponseBody", (object?)record.ResponseBody ?? DBNull.Value);
            command.Parameters.AddWithValue("@ResponseContentType", (object?)record.ResponseContentType ?? DBNull.Value);
            command.Parameters.AddWithValue("@ResponseBodySizeBytes", (object?)record.ResponseBodySizeBytes ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsSuccess", record.IsSuccess ? 1 : 0);
            command.Parameters.AddWithValue("@Exception", (object?)record.Exception ?? DBNull.Value);
            command.Parameters.AddWithValue("@TraceId", (object?)record.TraceId ?? DBNull.Value);
            command.Parameters.AddWithValue("@ParentSpanId", (object?)record.ParentSpanId ?? DBNull.Value);
            command.Parameters.AddWithValue("@InboundRequestPath", (object?)record.InboundRequestPath ?? DBNull.Value);
            command.Parameters.AddWithValue("@AttemptNumber", record.AttemptNumber);
            command.Parameters.AddWithValue("@RetryGroupId", (object?)record.RetryGroupId?.ToString("D", CultureInfo.InvariantCulture) ?? DBNull.Value);
            command.ExecuteNonQuery();

            EvictOverflow(connection);
        }

        OnRecordAdded?.Invoke(record);
    }

    /// <inheritdoc />
    public IReadOnlyList<HttpTrafficRecord> GetAll()
    {
        lock (_sync)
        {
            EnsureInitialized();
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM TrafficRecords ORDER BY Timestamp DESC;";
            using var reader = command.ExecuteReader();
            return ReadRecords(reader);
        }
    }

    /// <inheritdoc />
    public HttpTrafficRecord? GetById(Guid id)
    {
        lock (_sync)
        {
            EnsureInitialized();
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM TrafficRecords WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id.ToString("D", CultureInfo.InvariantCulture));
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadRecord(reader) : null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HttpTrafficRecord> GetByRetryGroupId(Guid groupId)
    {
        lock (_sync)
        {
            EnsureInitialized();
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT * FROM TrafficRecords WHERE RetryGroupId = @GroupId ORDER BY Timestamp DESC;";
            command.Parameters.AddWithValue("@GroupId", groupId.ToString("D", CultureInfo.InvariantCulture));
            using var reader = command.ExecuteReader();
            return ReadRecords(reader);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_sync)
        {
            EnsureInitialized();
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TrafficRecords;";
            command.ExecuteNonQuery();
        }
    }

    private static string BuildConnectionString(string databasePath)
    {
        if (!string.Equals(databasePath, ":memory:", StringComparison.Ordinal))
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return $"Data Source={databasePath};Cache=Shared";
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        using var connection = CreateConnection();
        EnsureSchema(connection);
        _isInitialized = true;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var createSchemaTable = connection.CreateCommand();
        createSchemaTable.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL
            );
            """;
        createSchemaTable.ExecuteNonQuery();

        using var getVersion = connection.CreateCommand();
        getVersion.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        var currentVersionObj = getVersion.ExecuteScalar();

        var currentVersion = currentVersionObj is null or DBNull
            ? 0
            : Convert.ToInt32(currentVersionObj, CultureInfo.InvariantCulture);

        if (currentVersion == 0)
        {
            using var seedVersion = connection.CreateCommand();
            seedVersion.CommandText = "INSERT INTO schema_version (version) VALUES (0);";
            seedVersion.ExecuteNonQuery();
        }

        if (currentVersion < CurrentSchemaVersion)
        {
            using var migration = connection.CreateCommand();
            migration.CommandText =
                """
                CREATE TABLE IF NOT EXISTS TrafficRecords (
                  Id TEXT PRIMARY KEY,
                  Timestamp TEXT NOT NULL,
                  Duration TEXT NOT NULL,
                  HttpClientName TEXT NOT NULL,
                  RequestMethod TEXT NOT NULL,
                  RequestUri TEXT NOT NULL,
                  RequestHeaders TEXT,
                  RequestBody TEXT,
                  RequestContentType TEXT,
                  RequestBodySizeBytes INTEGER,
                  ResponseStatusCode INTEGER,
                  ResponseHeaders TEXT,
                  ResponseBody TEXT,
                  ResponseContentType TEXT,
                  ResponseBodySizeBytes INTEGER,
                  IsSuccess INTEGER NOT NULL,
                  Exception TEXT,
                  TraceId TEXT,
                  ParentSpanId TEXT,
                  InboundRequestPath TEXT,
                  AttemptNumber INTEGER NOT NULL DEFAULT 1,
                  RetryGroupId TEXT
                );
                CREATE INDEX IF NOT EXISTS IX_TrafficRecords_Timestamp ON TrafficRecords(Timestamp);
                CREATE INDEX IF NOT EXISTS IX_TrafficRecords_RetryGroupId ON TrafficRecords(RetryGroupId);
                UPDATE schema_version SET version = 1;
                """;
            migration.ExecuteNonQuery();
        }
    }

    private void EvictOverflow(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM TrafficRecords
            WHERE Id IN (
                SELECT Id
                FROM TrafficRecords
                ORDER BY Timestamp DESC
                LIMIT -1 OFFSET @MaxRecords
            );
            """;
        command.Parameters.AddWithValue("@MaxRecords", _options.MaxStoredRecords);
        command.ExecuteNonQuery();
    }

    private static List<HttpTrafficRecord> ReadRecords(SqliteDataReader reader)
    {
        var records = new List<HttpTrafficRecord>();
        while (reader.Read())
        {
            records.Add(ReadRecord(reader));
        }

        return records;
    }

    private static HttpTrafficRecord ReadRecord(SqliteDataReader reader)
    {
        return new HttpTrafficRecord
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
            Timestamp = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("Timestamp")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Duration = TimeSpan.Parse(reader.GetString(reader.GetOrdinal("Duration")), CultureInfo.InvariantCulture),
            HttpClientName = reader.GetString(reader.GetOrdinal("HttpClientName")),
            RequestMethod = reader.GetString(reader.GetOrdinal("RequestMethod")),
            RequestUri = reader.GetString(reader.GetOrdinal("RequestUri")),
            RequestHeaders = DeserializeHeaders(reader, "RequestHeaders"),
            RequestBody = ReadNullableString(reader, "RequestBody"),
            RequestContentType = ReadNullableString(reader, "RequestContentType"),
            RequestBodySizeBytes = ReadNullableLong(reader, "RequestBodySizeBytes"),
            ResponseStatusCode = ReadNullableInt(reader, "ResponseStatusCode"),
            ResponseHeaders = DeserializeHeaders(reader, "ResponseHeaders"),
            ResponseBody = ReadNullableString(reader, "ResponseBody"),
            ResponseContentType = ReadNullableString(reader, "ResponseContentType"),
            ResponseBodySizeBytes = ReadNullableLong(reader, "ResponseBodySizeBytes"),
            IsSuccess = reader.GetInt32(reader.GetOrdinal("IsSuccess")) == 1,
            Exception = ReadNullableString(reader, "Exception"),
            TraceId = ReadNullableString(reader, "TraceId"),
            ParentSpanId = ReadNullableString(reader, "ParentSpanId"),
            InboundRequestPath = ReadNullableString(reader, "InboundRequestPath"),
            AttemptNumber = reader.GetInt32(reader.GetOrdinal("AttemptNumber")),
            RetryGroupId = ReadNullableGuid(reader, "RetryGroupId")
        };
    }

    private static string SerializeHeaders(Dictionary<string, string[]> headers) =>
        JsonSerializer.Serialize(headers, JsonOptions);

    private static Dictionary<string, string[]> DeserializeHeaders(SqliteDataReader reader, string columnName)
    {
        var json = ReadNullableString(reader, columnName);
        if (string.IsNullOrEmpty(json))
            return [];

        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, JsonOptions) ?? [];
    }

    private static string? ReadNullableString(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? ReadNullableInt(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long? ReadNullableLong(SqliteDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static Guid? ReadNullableGuid(SqliteDataReader reader, string columnName)
    {
        var value = ReadNullableString(reader, columnName);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

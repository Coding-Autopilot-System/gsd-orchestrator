using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GsdOrchestrator.Scheduling;

public sealed class SqliteGoalStore : IGoalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly ILogger<SqliteGoalStore> _logger;

    private static readonly (string Table, string Key)[] ProjectionTables =
    [
        ("work_items", "id"), ("dependencies", "id"), ("attempts", "id"),
        ("leases", "id"), ("budget_reservations", "id"), ("evidence", "id"),
        ("transitions", "id"), ("goal_events", "id"), ("idempotency_keys", "id")
    ];

    public SqliteGoalStore(string databasePath, ILogger<SqliteGoalStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;
            CREATE TABLE IF NOT EXISTS goals (id TEXT PRIMARY KEY, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS work_items (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS dependencies (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS attempts (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS leases (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS budget_reservations (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS evidence (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS transitions (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS goal_events (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS idempotency_keys (id TEXT PRIMARY KEY, goal_id TEXT NOT NULL, json TEXT NOT NULL, FOREIGN KEY(goal_id) REFERENCES goals(id) ON DELETE CASCADE);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();
        await UpsertAsync(connection, transaction, "goals", aggregate.Goal.Id, null, aggregate.Goal, cancellationToken);
        foreach (var table in ProjectionTables)
            await DeleteGoalRowsAsync(connection, transaction, table.Table, aggregate.Goal.Id, cancellationToken);

        await InsertMany(connection, transaction, "work_items", aggregate.Goal.Id, aggregate.WorkItems, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "dependencies", aggregate.Goal.Id, aggregate.Dependencies, item => $"{item.WorkItemId}:{item.DependsOnWorkItemId}", cancellationToken);
        await InsertMany(connection, transaction, "attempts", aggregate.Goal.Id, aggregate.Attempts, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "leases", aggregate.Goal.Id, aggregate.Leases, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "budget_reservations", aggregate.Goal.Id, aggregate.BudgetReservations, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "evidence", aggregate.Goal.Id, aggregate.Evidence, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "transitions", aggregate.Goal.Id, aggregate.Transitions, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "goal_events", aggregate.Goal.Id, aggregate.Events, item => item.Id, cancellationToken);
        await InsertMany(connection, transaction, "idempotency_keys", aggregate.Goal.Id, aggregate.IdempotencyKeys, item => item.Key, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogDebug("Persisted goal aggregate {GoalId}", aggregate.Goal.Id);
    }

    public async Task<GoalAggregate?> LoadAsync(string goalId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var goal = await LoadOneAsync<GoalRecord>(connection, "goals", goalId, cancellationToken);
        if (goal is null) return null;
        return new GoalAggregate(goal,
            await LoadManyAsync<WorkItemRecord>(connection, "work_items", goalId, cancellationToken),
            await LoadManyAsync<DependencyRecord>(connection, "dependencies", goalId, cancellationToken),
            await LoadManyAsync<AttemptRecord>(connection, "attempts", goalId, cancellationToken),
            await LoadManyAsync<LeaseRecord>(connection, "leases", goalId, cancellationToken),
            await LoadManyAsync<BudgetReservationRecord>(connection, "budget_reservations", goalId, cancellationToken),
            await LoadManyAsync<EvidenceRecord>(connection, "evidence", goalId, cancellationToken),
            await LoadManyAsync<TransitionRecord>(connection, "transitions", goalId, cancellationToken),
            await LoadManyAsync<GoalEventRecord>(connection, "goal_events", goalId, cancellationToken),
            await LoadManyAsync<IdempotencyRecord>(connection, "idempotency_keys", goalId, cancellationToken));
    }

    public async Task<DatabaseSettings> GetDatabaseSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        async Task<string> Scalar(string sql) { await using var command = connection.CreateCommand(); command.CommandText = sql; return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))!; }
        return new DatabaseSettings(await Scalar("PRAGMA journal_mode;"), await Scalar("PRAGMA foreign_keys;") == "1");
    }

    public async Task<LeaseRecord?> TryAcquireLeaseAsync(LeaseRequest request, CancellationToken cancellationToken = default)
    {
        if (request.GlobalLimit < 1 || request.ProviderLimit < 1 || request.RepositoryLimit < 1)
            throw new ArgumentOutOfRangeException(nameof(request), "Concurrency limits must be positive.");
        if (request.ExpiresAt <= request.AcquiredAt)
            throw new ArgumentException("Lease expiry must be after acquisition.", nameof(request));

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var item = await LoadProjectionAsync<WorkItemRecord>(connection, transaction, "work_items", request.WorkItemId, cancellationToken);
        if (item is null || item.GoalId != request.GoalId || item.Status is not WorkItemStatus.Ready and not WorkItemStatus.Pending)
            return null;

        var dependencies = (await LoadAllAsync<DependencyRecord>(connection, transaction, "dependencies", cancellationToken))
            .Where(dependency => dependency.GoalId == request.GoalId && dependency.WorkItemId == request.WorkItemId);
        var allItems = await LoadAllAsync<WorkItemRecord>(connection, transaction, "work_items", cancellationToken);
        if (dependencies.Any(dependency => allItems.SingleOrDefault(candidate => candidate.Id == dependency.DependsOnWorkItemId)?.Status != WorkItemStatus.Succeeded))
            return null;

        var activeLeases = (await LoadAllAsync<LeaseRecord>(connection, transaction, "leases", cancellationToken))
            .Where(lease => lease.ExpiresAt > request.AcquiredAt).ToList();
        var leasedItems = activeLeases.Join(allItems, lease => lease.WorkItemId, work => work.Id, (_, work) => work).ToList();
        if (activeLeases.Count >= request.GlobalLimit ||
            leasedItems.Count(work => work.Provider == item.Provider) >= request.ProviderLimit ||
            leasedItems.Count(work => work.Repository == item.Repository) >= request.RepositoryLimit)
            return null;

        var lease = new LeaseRecord(Guid.NewGuid().ToString("N"), request.GoalId, request.WorkItemId, request.Owner, request.AcquiredAt, request.ExpiresAt);
        await UpsertAsync(connection, transaction, "leases", lease.Id, request.GoalId, lease, cancellationToken);
        await UpsertAsync(connection, transaction, "budget_reservations", $"budget:{lease.Id}", request.GoalId,
            new BudgetReservationRecord($"budget:{lease.Id}", request.GoalId, request.WorkItemId, item.Provider, item.Repository, 0, 1), cancellationToken);
        await UpsertAsync(connection, transaction, "work_items", item.Id, request.GoalId, item with { Status = WorkItemStatus.Running }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return lease;
    }

    public async Task<int> RecoverExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var expired = (await LoadAllAsync<LeaseRecord>(connection, transaction, "leases", cancellationToken)).Where(lease => lease.ExpiresAt <= now).ToList();
        foreach (var lease in expired)
        {
            var item = await LoadProjectionAsync<WorkItemRecord>(connection, transaction, "work_items", lease.WorkItemId, cancellationToken);
            if (item is not null && item.Status == WorkItemStatus.Running)
                await UpsertAsync(connection, transaction, "work_items", item.Id, item.GoalId, item with { Status = WorkItemStatus.Ready }, cancellationToken);
            await DeleteByIdAsync(connection, transaction, "leases", lease.Id, cancellationToken);
            await DeleteByIdAsync(connection, transaction, "budget_reservations", $"budget:{lease.Id}", cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return expired.Count;
    }

    public async Task<bool> TryReserveIdempotencyKeyAsync(string goalId, string workItemId, string key, string effectType, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO idempotency_keys(id,goal_id,json) VALUES($id,$goal,$json)";
        command.Parameters.AddWithValue("$id", key); command.Parameters.AddWithValue("$goal", goalId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(new IdempotencyRecord(key, goalId, workItemId, effectType, DateTimeOffset.UtcNow), JsonOptions));
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    internal async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task InsertMany<T>(SqliteConnection c, SqliteTransaction t, string table, string goalId, IEnumerable<T> items, Func<T, string> key, CancellationToken ct)
    { foreach (var item in items) await UpsertAsync(c, t, table, key(item), goalId, item, ct); }

    private static async Task UpsertAsync<T>(SqliteConnection c, SqliteTransaction t, string table, string id, string? goalId, T value, CancellationToken ct)
    {
        await using var command = c.CreateCommand(); command.Transaction = t;
        command.CommandText = goalId is null
            ? $"INSERT OR REPLACE INTO {table}(id,json) VALUES($id,$json)"
            : $"INSERT OR REPLACE INTO {table}(id,goal_id,json) VALUES($id,$goal,$json)";
        command.Parameters.AddWithValue("$id", id); if (goalId is not null) command.Parameters.AddWithValue("$goal", goalId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(value, JsonOptions));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteGoalRowsAsync(SqliteConnection c, SqliteTransaction t, string table, string goalId, CancellationToken ct)
    { await using var command = c.CreateCommand(); command.Transaction=t; command.CommandText=$"DELETE FROM {table} WHERE goal_id=$goal"; command.Parameters.AddWithValue("$goal",goalId); await command.ExecuteNonQueryAsync(ct); }

    private static async Task<T?> LoadOneAsync<T>(SqliteConnection c, string table, string id, CancellationToken ct)
    { await using var command=c.CreateCommand(); command.CommandText=$"SELECT json FROM {table} WHERE id=$id"; command.Parameters.AddWithValue("$id",id); var json=Convert.ToString(await command.ExecuteScalarAsync(ct)); return json is null ? default : JsonSerializer.Deserialize<T>(json,JsonOptions); }

    private static async Task<IReadOnlyList<T>> LoadManyAsync<T>(SqliteConnection c, string table, string goalId, CancellationToken ct)
    { await using var command=c.CreateCommand(); command.CommandText=$"SELECT json FROM {table} WHERE goal_id=$goal ORDER BY id"; command.Parameters.AddWithValue("$goal",goalId); await using var reader=await command.ExecuteReaderAsync(ct); var result=new List<T>(); while(await reader.ReadAsync(ct)) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0),JsonOptions)!); return result; }

    private static async Task<T?> LoadProjectionAsync<T>(SqliteConnection c, SqliteTransaction t, string table, string id, CancellationToken ct)
    { await using var command=c.CreateCommand(); command.Transaction=t; command.CommandText=$"SELECT json FROM {table} WHERE id=$id"; command.Parameters.AddWithValue("$id",id); var json=Convert.ToString(await command.ExecuteScalarAsync(ct)); return json is null ? default : JsonSerializer.Deserialize<T>(json,JsonOptions); }

    private static async Task<IReadOnlyList<T>> LoadAllAsync<T>(SqliteConnection c, SqliteTransaction t, string table, CancellationToken ct)
    { await using var command=c.CreateCommand(); command.Transaction=t; command.CommandText=$"SELECT json FROM {table} ORDER BY id"; await using var reader=await command.ExecuteReaderAsync(ct); var result=new List<T>(); while(await reader.ReadAsync(ct)) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0),JsonOptions)!); return result; }

    private static async Task DeleteByIdAsync(SqliteConnection c, SqliteTransaction t, string table, string id, CancellationToken ct)
    { await using var command=c.CreateCommand(); command.Transaction=t; command.CommandText=$"DELETE FROM {table} WHERE id=$id"; command.Parameters.AddWithValue("$id",id); await command.ExecuteNonQueryAsync(ct); }
}

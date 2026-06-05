using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TokenSaverViewer;

var results = new List<(string Name, bool Passed, string? Error)>();

await RunAsync("Retention_BelowLimit_NothingPruned",                          Retention_BelowLimit_NothingPruned);
await RunAsync("Retention_AboveLimit_OldestRowsDeleted",                       Retention_AboveLimit_OldestRowsDeleted);
await RunAsync("Retention_SnapshotAccumulates_AcrossMultiplePrunes",           Retention_SnapshotAccumulates_AcrossMultiplePrunes);
await RunAsync("Retention_TotalTokensSaved_PreservedAfterPrune",               Retention_TotalTokensSaved_PreservedAfterPrune);
await RunAsync("Retention_PerToolLanguage_BreakdownPreservedAfterPrune",       Retention_PerToolLanguage_BreakdownPreservedAfterPrune);
await RunAsync("Retention_ZeroMaxRows_Disabled",                               Retention_ZeroMaxRows_Disabled);
await RunAsync("Retention_ExistingDb_NoSnapshotTable_MigrationThenPrune",      Retention_ExistingDb_NoSnapshotTable_MigrationThenPrune);
await RunAsync("Migration_ExistingDb_NoMcpVersionColumn_AddedAndDataPreserved", Migration_ExistingDb_NoMcpVersionColumn_AddedAndDataPreserved);

Console.WriteLine();
Console.WriteLine("== Viewer test results ==");
int passed = 0, failed = 0;
foreach (var (name, ok, err) in results)
{
    if (ok) { Console.WriteLine($"  PASS  {name}"); passed++; }
    else    { Console.WriteLine($"  FAIL  {name}\n        {err}"); failed++; }
}
Console.WriteLine($"\n{passed} passed, {failed} failed.");
return failed > 0 ? 1 : 0;

// ---------------------------------------------------------------------------

static ReportsDb CreateDb(SqliteConnection conn)
{
    var options = new DbContextOptionsBuilder<ReportsDb>()
        .UseSqlite(conn)
        .Options;
    var db = new ReportsDb(options);
    db.Database.EnsureCreated();
    return db;
}

static ReportRow MakeRow(int withoutTool, int withTool, DateTime receivedUtc,
    string toolName = "T", string language = "C#") => new()
{
    ToolName = toolName,
    Language = language,
    TokensWithoutTool = withoutTool,
    TokensWithTool = withTool,
    ReceivedUtc = receivedUtc,
};

static async Task<ToolLanguageSnapshot?> FindSnapshot(ReportsDb db, string tool, string lang)
    => await db.ToolLanguageSnapshots.FindAsync(new object?[] { tool, lang });

async Task RunAsync(string name, Func<Task> test)
{
    try { await test(); results.Add((name, true, null)); }
    catch (Exception ex) { results.Add((name, false, ex.Message)); }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

static async Task Retention_BelowLimit_NothingPruned()
{
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();
    using var db = CreateDb(conn);

    var now = DateTime.UtcNow;
    for (int i = 0; i < 5; i++)
        db.Reports.Add(MakeRow(100, 50, now.AddDays(-i)));
    await db.SaveChangesAsync();

    await DataRetentionService.PruneAsync(db, maxRows: 10);

    Assert(await db.Reports.CountAsync() == 5, "Expected 5 rows to remain");
    Assert(!await db.ToolLanguageSnapshots.AnyAsync(), "Snapshot table should be empty when nothing was pruned");
}

static async Task Retention_AboveLimit_OldestRowsDeleted()
{
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();
    using var db = CreateDb(conn);

    var now = DateTime.UtcNow;
    for (int i = 0; i < 15; i++)
        db.Reports.Add(MakeRow(100, 60, now.AddDays(-i)));
    await db.SaveChangesAsync();

    await DataRetentionService.PruneAsync(db, maxRows: 10);

    Assert(await db.Reports.CountAsync() == 10, "Expected exactly 10 rows after pruning");

    var oldest = await db.Reports.OrderBy(r => r.ReceivedUtc).FirstAsync();
    Assert(oldest.ReceivedUtc >= now.AddDays(-10), "Oldest remaining row should not predate the cutoff");
}

static async Task Retention_SnapshotAccumulates_AcrossMultiplePrunes()
{
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();
    using var db = CreateDb(conn);

    var now = DateTime.UtcNow;

    // First batch: 15 rows, prune to 10 → 5 pruned
    for (int i = 0; i < 15; i++)
        db.Reports.Add(MakeRow(100, 60, now.AddDays(-100 - i)));
    await db.SaveChangesAsync();
    await DataRetentionService.PruneAsync(db, maxRows: 10);

    // Add 8 more rows, now 18 total → prune to 10 → 8 pruned
    for (int i = 0; i < 8; i++)
        db.Reports.Add(MakeRow(200, 80, now.AddDays(-i)));
    await db.SaveChangesAsync();
    await DataRetentionService.PruneAsync(db, maxRows: 10);

    var totalRunCount = await db.ToolLanguageSnapshots.SumAsync(s => s.RunCount);
    Assert(totalRunCount == 13, $"Expected total snapshot RunCount=13, got {totalRunCount}");
    Assert(await db.Reports.CountAsync() == 10, "Expected 10 rows to remain");
}

static async Task Retention_TotalTokensSaved_PreservedAfterPrune()
{
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();
    using var db = CreateDb(conn);

    var now = DateTime.UtcNow;
    for (int i = 0; i < 20; i++)
        db.Reports.Add(MakeRow(1000, 400, now.AddDays(-i)));
    await db.SaveChangesAsync();

    var totalBefore = await db.Reports.SumAsync(r => (long)(r.TokensWithoutTool - r.TokensWithTool));

    await DataRetentionService.PruneAsync(db, maxRows: 10);

    var snapWithout = await db.ToolLanguageSnapshots.SumAsync(s => s.TokensWithoutTotal);
    var snapWith    = await db.ToolLanguageSnapshots.SumAsync(s => s.TokensWithTotal);
    var liveWithout = await db.Reports.SumAsync(r => (long)r.TokensWithoutTool);
    var liveWith    = await db.Reports.SumAsync(r => (long)r.TokensWithTool);
    var totalAfter  = (snapWithout - snapWith) + (liveWithout - liveWith);

    Assert(totalAfter == totalBefore,
        $"Total tokens saved should be preserved: before={totalBefore}, after={totalAfter}");
}

static async Task Retention_PerToolLanguage_BreakdownPreservedAfterPrune()
{
    // Insert rows for two distinct (tool, language) combos, prune, and verify that
    // the per-tool snapshot correctly captures each combo's totals independently.
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();
    using var db = CreateDb(conn);

    var now = DateTime.UtcNow;

    // 10 rows for FocusMethod / C# — without=2000, with=800 → saved=1200 each
    for (int i = 0; i < 10; i++)
        db.Reports.Add(MakeRow(2000, 800, now.AddDays(-20 - i), "FocusMethod", "C#"));

    // 10 rows for MinifyFile / TypeScript — without=500, with=300 → saved=200 each
    for (int i = 0; i < 10; i++)
        db.Reports.Add(MakeRow(500, 300, now.AddDays(-10 - i), "MinifyFile", "TypeScript"));

    // 5 recent rows for FocusMethod / C# that should survive pruning
    for (int i = 0; i < 5; i++)
        db.Reports.Add(MakeRow(2000, 800, now.AddDays(-i), "FocusMethod", "C#"));

    await db.SaveChangesAsync(); // 25 rows total

    // Prune to 10 → 15 oldest rows deleted:
    //   10 × FocusMethod/C# (without=2000, with=800) → snapshot saved=12000
    //   5  × MinifyFile/TypeScript (without=500, with=300) → snapshot saved=1000
    await DataRetentionService.PruneAsync(db, maxRows: 10);

    Assert(await db.Reports.CountAsync() == 10, "Expected 10 live rows");

    var focusSnap = await FindSnapshot(db, "FocusMethod", "C#");
    Assert(focusSnap is not null, "Snapshot must exist for FocusMethod/C#");
    Assert(focusSnap!.TokensWithoutTotal == 10 * 2000, $"FocusMethod without: expected {10 * 2000}, got {focusSnap.TokensWithoutTotal}");
    Assert(focusSnap.TokensWithTotal    == 10 * 800,  $"FocusMethod with: expected {10 * 800}, got {focusSnap.TokensWithTotal}");
    Assert(focusSnap.RunCount           == 10,        $"FocusMethod RunCount: expected 10, got {focusSnap.RunCount}");

    var minifySnap = await FindSnapshot(db, "MinifyFile", "TypeScript");
    Assert(minifySnap is not null, "Snapshot must exist for MinifyFile/TypeScript");
    Assert(minifySnap!.TokensWithoutTotal == 5 * 500, $"MinifyFile without: expected {5 * 500}, got {minifySnap.TokensWithoutTotal}");
    Assert(minifySnap.TokensWithTotal    == 5 * 300,  $"MinifyFile with: expected {5 * 300}, got {minifySnap.TokensWithTotal}");
    Assert(minifySnap.RunCount           == 5,        $"MinifyFile RunCount: expected 5, got {minifySnap.RunCount}");

    // Verify combined total is preserved
    var snapSaved = (focusSnap.TokensWithoutTotal - focusSnap.TokensWithTotal)
                  + (minifySnap.TokensWithoutTotal - minifySnap.TokensWithTotal);
    var liveWithout = await db.Reports.SumAsync(r => (long)r.TokensWithoutTool);
    var liveWith    = await db.Reports.SumAsync(r => (long)r.TokensWithTool);
    var totalAfter  = snapSaved + (liveWithout - liveWith);
    var totalBefore = 10L * (2000 - 800) + 10L * (500 - 300) + 5L * (2000 - 800); // 12000+2000+6000=20000
    Assert(totalAfter == totalBefore, $"Combined total should be preserved: expected={totalBefore}, got={totalAfter}");
}

static async Task Retention_ZeroMaxRows_Disabled()
{
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();
    using var db = CreateDb(conn);

    var now = DateTime.UtcNow;
    for (int i = 0; i < 100; i++)
        db.Reports.Add(MakeRow(100, 50, now.AddDays(-i)));
    await db.SaveChangesAsync();

    await DataRetentionService.PruneAsync(db, maxRows: 0);

    Assert(await db.Reports.CountAsync() == 100, "MaxReportRows=0 should disable pruning");
}

static async Task Retention_ExistingDb_NoSnapshotTable_MigrationThenPrune()
{
    // Simulates a Pi with an existing DB that has Reports rows but no ToolLanguageSnapshots table.
    // The startup DDL in Program.cs adds the table; we replicate that here before building
    // the EF context so EnsureCreated sees it as already present.
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();

    // Create only the legacy schema (Reports + its indexes, no snapshot table)
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE Reports (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ToolName TEXT NOT NULL,
                Language TEXT NOT NULL,
                TokensWithoutTool INTEGER NOT NULL,
                TokensWithTool INTEGER NOT NULL,
                Notes TEXT,
                ClientId TEXT,
                ReceivedUtc TEXT NOT NULL
            );
            CREATE INDEX IX_Reports_ToolName_Language ON Reports (ToolName, Language);
            CREATE INDEX IX_Reports_ReceivedUtc ON Reports (ReceivedUtc);
            """;
        cmd.ExecuteNonQuery();
    }

    // Seed 20 rows representing existing data (without=1000, with=300 → saved=700 each)
    var now = DateTime.UtcNow;
    using (var cmd = conn.CreateCommand())
    {
        for (int i = 0; i < 20; i++)
        {
            cmd.CommandText = $"""
                INSERT INTO Reports (ToolName, Language, TokensWithoutTool, TokensWithTool, Notes, ClientId, ReceivedUtc)
                VALUES ('T', 'C#', 1000, 300, NULL, NULL, '{now.AddDays(-i):O}')
                """;
            cmd.ExecuteNonQuery();
        }
    }

    var totalBefore = 20L * (1000 - 300); // 14000

    // Run the startup migration that Program.cs executes on an existing DB
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ToolLanguageSnapshots (
                ToolName TEXT NOT NULL,
                Language TEXT NOT NULL,
                TokensWithoutTotal INTEGER NOT NULL DEFAULT 0,
                TokensWithTotal INTEGER NOT NULL DEFAULT 0,
                RunCount INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT PK_ToolLanguageSnapshots PRIMARY KEY (ToolName, Language)
            )
            """;
        cmd.ExecuteNonQuery();
    }

    // Program.cs also adds the McpVersion column to legacy Reports tables on startup.
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "ALTER TABLE Reports ADD COLUMN McpVersion TEXT";
        cmd.ExecuteNonQuery();
    }

    // Now build the EF context — EnsureCreated sees both tables already exist and does nothing
    var options = new DbContextOptionsBuilder<ReportsDb>().UseSqlite(conn).Options;
    using var db = new ReportsDb(options);
    db.Database.EnsureCreated();

    Assert(await db.Reports.CountAsync() == 20, "Pre-existing rows must all be present");
    Assert(!await db.ToolLanguageSnapshots.AnyAsync(), "Snapshot table should be empty before first prune");

    // Prune to 10 — the 10 oldest rows move to snapshot
    await DataRetentionService.PruneAsync(db, maxRows: 10);

    Assert(await db.ToolLanguageSnapshots.AnyAsync(), "Snapshot must be populated by first prune");
    Assert(await db.Reports.CountAsync() == 10, "10 live rows should remain");

    var snapWithout = await db.ToolLanguageSnapshots.SumAsync(s => s.TokensWithoutTotal);
    var snapWith    = await db.ToolLanguageSnapshots.SumAsync(s => s.TokensWithTotal);
    var liveWithout = await db.Reports.SumAsync(r => (long)r.TokensWithoutTool);
    var liveWith    = await db.Reports.SumAsync(r => (long)r.TokensWithTool);
    var totalAfter  = (snapWithout - snapWith) + (liveWithout - liveWith);

    Assert(totalAfter == totalBefore,
        $"Total tokens saved must equal pre-migration total: expected={totalBefore}, got={totalAfter}");
}

static async Task Migration_ExistingDb_NoMcpVersionColumn_AddedAndDataPreserved()
{
    // Simulates the Pi DB: a Reports table that predates the McpVersion column.
    // Replicates the pragma-guarded ALTER TABLE that Program.cs runs on startup
    // and verifies existing rows survive and new rows can store a version.
    using var conn = new SqliteConnection("Data Source=:memory:");
    conn.Open();

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = """
            CREATE TABLE Reports (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ToolName TEXT NOT NULL,
                Language TEXT NOT NULL,
                TokensWithoutTool INTEGER NOT NULL,
                TokensWithTool INTEGER NOT NULL,
                Notes TEXT,
                ClientId TEXT,
                ReceivedUtc TEXT NOT NULL
            );
            CREATE INDEX IX_Reports_ToolName_Language ON Reports (ToolName, Language);
            CREATE INDEX IX_Reports_ReceivedUtc ON Reports (ReceivedUtc);
            """;
        cmd.ExecuteNonQuery();
    }

    var now = DateTime.UtcNow;
    using (var cmd = conn.CreateCommand())
    {
        for (int i = 0; i < 5; i++)
        {
            cmd.CommandText = $"""
                INSERT INTO Reports (ToolName, Language, TokensWithoutTool, TokensWithTool, Notes, ClientId, ReceivedUtc)
                VALUES ('T', 'C#', 1000, 300, 'note{i}', 'client{i}', '{now.AddDays(-i):O}')
                """;
            cmd.ExecuteNonQuery();
        }
    }

    // Replicate the startup migration: only add the column when it's missing.
    static long McpVersionColumnCount(SqliteConnection c)
    {
        using var check = c.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Reports') WHERE name='McpVersion'";
        return (long)check.ExecuteScalar()!;
    }

    Assert(McpVersionColumnCount(conn) == 0, "Legacy DB must not have McpVersion before migration");

    using (var add = conn.CreateCommand())
    {
        add.CommandText = "ALTER TABLE Reports ADD COLUMN McpVersion TEXT";
        add.ExecuteNonQuery();
    }

    Assert(McpVersionColumnCount(conn) == 1, "McpVersion column must exist after migration");

    var options = new DbContextOptionsBuilder<ReportsDb>().UseSqlite(conn).Options;
    using var db = new ReportsDb(options);
    db.Database.EnsureCreated(); // must be a no-op against the already-present schema

    Assert(await db.Reports.CountAsync() == 5, "All pre-existing rows must survive migration");
    Assert(await db.Reports.AllAsync(r => r.McpVersion == null), "Legacy rows must have null McpVersion");

    var legacy = await db.Reports.OrderByDescending(r => r.ReceivedUtc).FirstAsync();
    Assert(legacy.Notes == "note0" && legacy.ClientId == "client0",
        "Existing column data must be intact after migration");

    // A new row carrying a version round-trips through EF.
    db.Reports.Add(new ReportRow
    {
        ToolName = "FocusMethod",
        Language = "C#",
        TokensWithoutTool = 900,
        TokensWithTool = 200,
        McpVersion = "1.13.2",
        ReceivedUtc = now,
    });
    await db.SaveChangesAsync();

    var saved = await db.Reports.SingleAsync(r => r.McpVersion == "1.13.2");
    Assert(saved.ToolName == "FocusMethod", "New row with McpVersion must round-trip");
}

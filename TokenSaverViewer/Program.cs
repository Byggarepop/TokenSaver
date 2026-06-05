using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TokenSaverViewer;
using TokenSaverViewer.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration["TokenSaver:DbPath"]
             ?? Path.Combine(AppContext.BaseDirectory, "data", "tokensaver.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<ReportsDb>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<EmailNotificationService>();
builder.Services.AddHostedService<DataRetentionService>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("ingest", ctx =>
    {
        var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

// Apply schema on startup. EnsureCreated is fine for a single-table append-only store;
// graduate to migrations if the schema starts evolving.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReportsDb>();
    db.Database.EnsureCreated();

    // GDPR cleanup: drop ClientIpHash if it exists on an already-deployed DB.
    // EnsureCreated never alters existing schemas, so this must run explicitly.
    var conn = db.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
        conn.Open();
    using var check = conn.CreateCommand();
    check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Reports') WHERE name='ClientIpHash'";
    var columnExists = (long)check.ExecuteScalar()! > 0;
    if (columnExists)
    {
        if (File.Exists(dbPath))
        {
            var backupPath = dbPath + $".bak-{DateTime.UtcNow:yyyyMMddHHmmss}";
            using var backup = conn.CreateCommand();
            backup.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}'";
            backup.ExecuteNonQuery();
        }

        using var drop = conn.CreateCommand();
        drop.CommandText = "ALTER TABLE Reports DROP COLUMN ClientIpHash";
        drop.ExecuteNonQuery();
    }

    // Add ToolLanguageSnapshots table for existing DBs (EnsureCreated won't alter an existing schema).
    using var createSnapshot = conn.CreateCommand();
    createSnapshot.CommandText = """
        CREATE TABLE IF NOT EXISTS ToolLanguageSnapshots (
            ToolName TEXT NOT NULL,
            Language TEXT NOT NULL,
            TokensWithoutTotal INTEGER NOT NULL DEFAULT 0,
            TokensWithTotal INTEGER NOT NULL DEFAULT 0,
            RunCount INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT PK_ToolLanguageSnapshots PRIMARY KEY (ToolName, Language)
        )
        """;
    createSnapshot.ExecuteNonQuery();

    // Add McpVersion column for existing DBs (EnsureCreated won't alter an
    // existing schema). SQLite has no ADD COLUMN IF NOT EXISTS, so guard on
    // pragma_table_info. The column is nullable, so existing rows are untouched.
    using var versionCheck = conn.CreateCommand();
    versionCheck.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Reports') WHERE name='McpVersion'";
    var versionColumnExists = (long)versionCheck.ExecuteScalar()! > 0;
    if (!versionColumnExists)
    {
        using var addVersion = conn.CreateCommand();
        addVersion.CommandText = "ALTER TABLE Reports ADD COLUMN McpVersion TEXT";
        addVersion.ExecuteNonQuery();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseRateLimiter();

app.MapStaticAssets();

// ---- Public ingest + stats API ----------------------------------------------

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/reports", async (ReportPostDto dto, HttpContext http, ReportsDb db, EmailNotificationService email) =>
{
    if (string.IsNullOrWhiteSpace(dto.ToolName) || dto.ToolName.Length > 64)
        return Results.BadRequest(new { error = "ToolName required, max 64 chars." });
    if (string.IsNullOrWhiteSpace(dto.Language) || dto.Language.Length > 32)
        return Results.BadRequest(new { error = "Language required, max 32 chars." });
    if (dto.TokensWithoutTool < 0 || dto.TokensWithTool < 0)
        return Results.BadRequest(new { error = "Token counts must be non-negative." });
    if (dto.TokensWithTool > dto.TokensWithoutTool)
        return Results.BadRequest(new { error = "TokensWithTool cannot exceed TokensWithoutTool." });
    const int Cap = 10_000_000;
    if (dto.TokensWithoutTool > Cap)
        return Results.BadRequest(new { error = $"TokensWithoutTool exceeds cap of {Cap}." });

    var notes = dto.Notes?.Length > 200 ? dto.Notes[..200] : dto.Notes;
    var clientId = dto.ClientId?.Length > 64 ? dto.ClientId[..64] : dto.ClientId;
    var mcpVersion = dto.McpVersion?.Length > 32 ? dto.McpVersion[..32] : dto.McpVersion;

    var isNewClient = clientId is not null && !await db.Reports.AnyAsync(r => r.ClientId == clientId);

    var row = new ReportRow
    {
        ToolName = dto.ToolName.Trim(),
        Language = dto.Language.Trim(),
        TokensWithoutTool = dto.TokensWithoutTool,
        TokensWithTool = dto.TokensWithTool,
        Notes = notes,
        ClientId = clientId,
        McpVersion = mcpVersion,
        ReceivedUtc = DateTime.UtcNow,
    };

    db.Reports.Add(row);
    await db.SaveChangesAsync();

    if (isNewClient)
        _ = email.SendNewClientNotificationAsync(clientId!);

    return Results.Created($"/api/reports/{row.Id}", new { id = row.Id });
})
.RequireRateLimiting("ingest");

app.MapGet("/api/stats/summary", async (ReportsDb db) =>
{
    var snapshots = await db.ToolLanguageSnapshots.ToListAsync();
    var snapWithout = snapshots.Sum(s => s.TokensWithoutTotal);
    var snapWith    = snapshots.Sum(s => s.TokensWithTotal);
    var snapRuns    = snapshots.Sum(s => s.RunCount);

    var agg = await db.Reports
        .GroupBy(_ => 1)
        .Select(g => new
        {
            RunCount = g.Count(),
            TokensWithout = g.Sum(r => (long)r.TokensWithoutTool),
            TokensWith = g.Sum(r => (long)r.TokensWithTool),
            FirstSeen = g.Min(r => r.ReceivedUtc),
            LastSeen = g.Max(r => r.ReceivedUtc),
            DistinctClients = g.Select(r => r.ClientId).Distinct().Count(),
        })
        .FirstOrDefaultAsync();

    var totalWithout = snapWithout + (agg?.TokensWithout ?? 0);
    var totalWith    = snapWith    + (agg?.TokensWith    ?? 0);
    var totalSaved   = totalWithout - totalWith;
    var totalRuns    = snapRuns     + (agg?.RunCount     ?? 0);
    var avgReduction = totalWithout == 0 ? 0 : (double)totalSaved / totalWithout * 100;

    return Results.Ok(new StatsSummary(
        (int)totalRuns,
        totalSaved,
        avgReduction,
        agg?.FirstSeen,
        agg?.LastSeen,
        agg?.DistinctClients ?? 0));
});

app.MapGet("/api/stats/by-tool-language", async (ReportsDb db) =>
{
    var snapshots = await db.ToolLanguageSnapshots.ToListAsync();
    var snapDict  = snapshots.ToDictionary(s => (s.ToolName, s.Language));

    var live = await db.Reports
        .GroupBy(r => new { r.ToolName, r.Language })
        .Select(g => new
        {
            g.Key.ToolName,
            g.Key.Language,
            RunCount = g.Count(),
            TokensWithout = g.Sum(r => (long)r.TokensWithoutTool),
            TokensWith    = g.Sum(r => (long)r.TokensWithTool),
        })
        .ToListAsync();
    var liveDict = live.ToDictionary(r => (r.ToolName, r.Language));

    var allKeys = snapDict.Keys.Union(liveDict.Keys);
    var rows = allKeys
        .Select(key =>
        {
            var s = snapDict.GetValueOrDefault(key);
            var l = liveDict.GetValueOrDefault(key);
            var without = (s?.TokensWithoutTotal ?? 0) + (l?.TokensWithout ?? 0);
            var with_   = (s?.TokensWithTotal    ?? 0) + (l?.TokensWith    ?? 0);
            var runs    = (s?.RunCount            ?? 0) + (l?.RunCount      ?? 0);
            var saved   = without - with_;
            var pct     = without == 0 ? 0 : (double)saved / without * 100;
            return new ToolLanguageRow(key.ToolName, key.Language, (int)runs, saved, pct);
        })
        .OrderByDescending(r => r.TokensSaved)
        .ToList();

    return Results.Ok(rows);
});

// ---- Blazor app -------------------------------------------------------------

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();


public sealed record ReportPostDto(
    string ToolName,
    string Language,
    int TokensWithoutTool,
    int TokensWithTool,
    string? Notes,
    string? ClientId,
    string? McpVersion);

public sealed record StatsSummary(
    int RunCount,
    long TokensSaved,
    double AvgReductionPercent,
    DateTime? FirstSeen,
    DateTime? LastSeen,
    int DistinctClients);

public sealed record ToolLanguageRow(
    string ToolName,
    string Language,
    int RunCount,
    long TokensSaved,
    double AvgReductionPercent);

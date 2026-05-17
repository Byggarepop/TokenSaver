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

app.MapPost("/api/reports", async (ReportPostDto dto, HttpContext http, ReportsDb db) =>
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

    var row = new ReportRow
    {
        ToolName = dto.ToolName.Trim(),
        Language = dto.Language.Trim(),
        TokensWithoutTool = dto.TokensWithoutTool,
        TokensWithTool = dto.TokensWithTool,
        Notes = notes,
        ClientId = clientId,
        ReceivedUtc = DateTime.UtcNow,
        ClientIpHash = HashIp(http.Connection.RemoteIpAddress?.ToString()),
    };

    db.Reports.Add(row);
    await db.SaveChangesAsync();
    return Results.Created($"/api/reports/{row.Id}", new { id = row.Id });
})
.RequireRateLimiting("ingest");

app.MapGet("/api/stats/summary", async (ReportsDb db) =>
{
    var agg = await db.Reports
        .GroupBy(_ => 1)
        .Select(g => new
        {
            RunCount = g.Count(),
            TokensSaved = g.Sum(r => (long)(r.TokensWithoutTool - r.TokensWithTool)),
            TokensWithout = g.Sum(r => (long)r.TokensWithoutTool),
            FirstSeen = g.Min(r => r.ReceivedUtc),
            LastSeen = g.Max(r => r.ReceivedUtc),
            DistinctClients = g.Select(r => r.ClientId).Distinct().Count(),
        })
        .FirstOrDefaultAsync();

    if (agg is null)
        return Results.Ok(new StatsSummary(0, 0, 0, null, null, 0));

    var avgReduction = agg.TokensWithout == 0 ? 0 : (double)agg.TokensSaved / agg.TokensWithout * 100;
    return Results.Ok(new StatsSummary(
        agg.RunCount,
        agg.TokensSaved,
        avgReduction,
        agg.FirstSeen,
        agg.LastSeen,
        agg.DistinctClients));
});

app.MapGet("/api/stats/by-tool-language", async (ReportsDb db) =>
{
    var raw = await db.Reports
        .GroupBy(r => new { r.ToolName, r.Language })
        .Select(g => new
        {
            g.Key.ToolName,
            g.Key.Language,
            RunCount = g.Count(),
            TokensWithout = g.Sum(r => (long)r.TokensWithoutTool),
            TokensWith = g.Sum(r => (long)r.TokensWithTool),
        })
        .ToListAsync();

    var rows = raw
        .Select(r =>
        {
            var saved = r.TokensWithout - r.TokensWith;
            var pct = r.TokensWithout == 0 ? 0 : (double)saved / r.TokensWithout * 100;
            return new ToolLanguageRow(r.ToolName, r.Language, r.RunCount, saved, pct);
        })
        .OrderByDescending(r => r.TokensSaved)
        .ToList();

    return Results.Ok(rows);
});

// ---- Blazor app -------------------------------------------------------------

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string? HashIp(string? ip)
{
    if (string.IsNullOrEmpty(ip)) return null;
    var salt = DateTime.UtcNow.ToString("yyyyMMdd");
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + ip));
    return Convert.ToHexString(bytes)[..16];
}

public sealed record ReportPostDto(
    string ToolName,
    string Language,
    int TokensWithoutTool,
    int TokensWithTool,
    string? Notes,
    string? ClientId);

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

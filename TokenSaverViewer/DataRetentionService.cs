using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TokenSaverViewer;

public sealed class DataRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly int _maxRows;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _maxRows = config.GetValue<int>("Retention:MaxReportRows", 0);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ReportsDb>();
                await PruneAsync(db, _maxRows, _logger, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Retention prune failed.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    internal static async Task PruneAsync(ReportsDb db, int maxRows, ILogger? logger = null, CancellationToken ct = default)
    {
        if (maxRows <= 0) return;

        var total = await db.Reports.CountAsync(ct);
        var excess = total - maxRows;
        if (excess <= 0) return;

        var toDelete = await db.Reports
            .OrderBy(r => r.ReceivedUtc)
            .Take(excess)
            .ToListAsync(ct);

        // Group deleted rows by (ToolName, Language) and upsert into the snapshot table
        // so the per-tool breakdown survives pruning.
        var groups = toDelete
            .GroupBy(r => (r.ToolName, r.Language))
            .Select(g => new
            {
                g.Key.ToolName,
                g.Key.Language,
                Without = g.Sum(r => (long)r.TokensWithoutTool),
                With = g.Sum(r => (long)r.TokensWithTool),
                Count = (long)g.Count(),
            });

        foreach (var group in groups)
        {
            var snap = await db.ToolLanguageSnapshots
                .FindAsync(new object?[] { group.ToolName, group.Language }, ct);

            if (snap is null)
            {
                db.ToolLanguageSnapshots.Add(new ToolLanguageSnapshot
                {
                    ToolName = group.ToolName,
                    Language = group.Language,
                    TokensWithoutTotal = group.Without,
                    TokensWithTotal = group.With,
                    RunCount = group.Count,
                });
            }
            else
            {
                snap.TokensWithoutTotal += group.Without;
                snap.TokensWithTotal += group.With;
                snap.RunCount += group.Count;
            }
        }

        db.Reports.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        logger?.LogInformation(
            "Retention: pruned {PrunedCount} rows, kept {MaxRows}.",
            toDelete.Count, maxRows);
    }
}

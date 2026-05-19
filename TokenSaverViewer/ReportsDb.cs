using Microsoft.EntityFrameworkCore;

namespace TokenSaverViewer;

public sealed class ReportsDb : DbContext
{
    public ReportsDb(DbContextOptions<ReportsDb> options) : base(options) { }

    public DbSet<ReportRow> Reports => Set<ReportRow>();
    public DbSet<ToolLanguageSnapshot> ToolLanguageSnapshots => Set<ToolLanguageSnapshot>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<ReportRow>(e =>
        {
            e.HasIndex(r => new { r.ToolName, r.Language });
            e.HasIndex(r => r.ReceivedUtc);
        });

        mb.Entity<ToolLanguageSnapshot>(e =>
            e.HasKey(s => new { s.ToolName, s.Language }));
    }
}

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
            // Idempotency: a re-sent row carries the same client-generated
            // EventId, so a unique index turns a duplicate POST into a no-op.
            // Filtered to non-null so legacy rows (EventId == null) are exempt.
            e.HasIndex(r => r.EventId)
                .IsUnique()
                .HasFilter("\"EventId\" IS NOT NULL");
        });

        mb.Entity<ToolLanguageSnapshot>(e =>
            e.HasKey(s => new { s.ToolName, s.Language }));
    }
}

using Microsoft.EntityFrameworkCore;
using VolleyballStatTracker.Models;

namespace VolleyballStatTracker.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Set> Sets => Set<Set>();
    public DbSet<StatEntry> StatEntries => Set<StatEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(player =>
        {
            player.Property(p => p.Name).HasMaxLength(Player.NameMaxLength);
            player.HasIndex(p => p.ShirtNumber).IsUnique();
            player.ToTable(t => t.HasCheckConstraint("CK_Players_ShirtNumber", "[ShirtNumber] BETWEEN 0 AND 99"));
        });

        modelBuilder.Entity<Match>()
            .Property(m => m.Opponent)
            .HasMaxLength(Match.OpponentMaxLength);

        modelBuilder.Entity<Set>(set =>
        {
            set.HasOne(s => s.Match)
                .WithMany(m => m.Sets)
                .HasForeignKey(s => s.MatchId);

            // Stops a double tap on "Start set 2" creating two Set 2 rows.
            set.HasIndex(s => new { s.MatchId, s.SetNumber }).IsUnique();
            set.ToTable(t => t.HasCheckConstraint("CK_Sets_SetNumber", $"[SetNumber] BETWEEN 1 AND {StatRules.MaxSets}"));
        });

        modelBuilder.Entity<StatEntry>(entry =>
        {
            entry.HasOne(e => e.Set)
                .WithMany(s => s.StatEntries)
                .HasForeignKey(e => e.SetId);

            entry.HasOne(e => e.Player)
                .WithMany(p => p.StatEntries)
                .HasForeignKey(e => e.PlayerId)
                .IsRequired(false);

            entry.ToTable(t => t.HasCheckConstraint("CK_StatEntries_Grade", "[Grade] IS NULL OR [Grade] BETWEEN 0 AND 3"));
        });
    }
}

using AvailabilityService.Domain;

using Microsoft.EntityFrameworkCore;

namespace AvailabilityService.Infrastructure;

public class AvailabilityDbContext : DbContext
{
    public AvailabilityDbContext(DbContextOptions<AvailabilityDbContext> options) : base(options) { }

    public DbSet<Availability> Availabilities => Set<Availability>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Availability>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.AccommodationId);
            entity.HasIndex(a => new { a.AccommodationId, a.FromDate, a.ToDate });

            entity.Property(a => a.PriceModifiers).HasColumnType("jsonb");
            entity.Property(a => a.Price).HasPrecision(18, 2);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TrackableEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.ModifiedTimestamp = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

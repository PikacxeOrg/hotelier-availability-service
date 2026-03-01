using AvailabilityService.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using System.Text.Json;

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

            entity.Property(a => a.PriceModifiers)
                .HasConversion(
                    new ValueConverter<Dictionary<string, decimal>, string>(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (JsonSerializerOptions?)null) ?? new()),
                    new ValueComparer<Dictionary<string, decimal>>(
                        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                        v => new Dictionary<string, decimal>(v)))
                .HasColumnType("jsonb");
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

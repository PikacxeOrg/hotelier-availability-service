namespace AvailabilityService.Domain;

/// <summary>
/// Published when availability periods are created or updated.
/// Consumed by search-service to update availability in the index.
/// </summary>
public record AvailabilityUpdated
{
    public Guid AvailabilityId { get; init; }
    public Guid AccommodationId { get; init; }
    public DateTime FromDate { get; init; }
    public DateTime ToDate { get; init; }
    public decimal Price { get; init; }
    public bool IsAvailable { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

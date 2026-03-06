namespace Hotelier.Events;

/// <summary>
/// Published when availability periods are created or updated.
/// Consumed by search-service to update availability in the index.
/// </summary>
public record AvailabilityUpdated
{
    public Guid AvailabilityId { get; init; }
    public Guid AccommodationId { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public decimal Price { get; init; }
    public string PriceType { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

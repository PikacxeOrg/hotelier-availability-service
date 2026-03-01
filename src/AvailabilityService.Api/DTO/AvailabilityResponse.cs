using AvailabilityService.Domain;

namespace AvailabilityService.Api;

public class AvailabilityResponse
{
    public Guid Id { get; set; }
    public Guid AccommodationId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal Price { get; set; }
    public PriceType PriceType { get; set; }
    public Dictionary<string, decimal> PriceModifiers { get; set; } = new();
    public bool IsAvailable { get; set; }
}

using AvailabilityService.Domain;

namespace AvailabilityService.Api;

public class AvailabilityResponse
{
    public Guid Id { get; set; }
    public Guid AccommodationId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal Price { get; set; }
    public PriceType PriceType { get; set; }
    public Dictionary<string, decimal> PriceModifiers { get; set; } = new();
    public bool IsAvailable { get; set; }
}

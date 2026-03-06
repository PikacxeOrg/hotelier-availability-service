using System.ComponentModel.DataAnnotations;

using AvailabilityService.Domain;

namespace AvailabilityService.Api;

public class CreateAvailabilityRequest
{
    [Required]
    public Guid AccommodationId { get; set; }

    [Required]
    public DateOnly FromDate { get; set; }

    [Required]
    public DateOnly ToDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    public PriceType PriceType { get; set; }

    public Dictionary<string, decimal> PriceModifiers { get; set; } = new();
}

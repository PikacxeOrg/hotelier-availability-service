using System.ComponentModel.DataAnnotations;

using AvailabilityService.Domain;

namespace AvailabilityService.Api;

public class UpdateAvailabilityRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal? Price { get; set; }

    public PriceType? PriceType { get; set; }

    public Dictionary<string, decimal>? PriceModifiers { get; set; }

    public bool? IsAvailable { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace AvailabilityService.Domain;

public enum PriceType
{
    PerGuest,
    PerUnit
}

public class Availability : TrackableEntity
{
    [Required]
    public Guid AccommodationId { get; set; }

    [Required]
    public DateTime FromDate { get; set; }

    [Required]
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Base price per night (interpretation depends on PriceType).
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    /// <summary>
    /// Whether the price is per guest per night or per unit per night.
    /// </summary>
    [Required]
    public PriceType PriceType { get; set; }

    /// <summary>
    /// Price modifiers for variable pricing (e.g. "Weekend": 1.2, "Summer": 1.5).
    /// Stored as a JSON column. Keys are modifier names, values are multipliers.
    /// </summary>
    public Dictionary<string, decimal> PriceModifiers { get; set; } = new();

    public bool IsAvailable { get; set; } = true;
}

using System.ComponentModel.DataAnnotations;

namespace AvailabilityService.Api;

public class CheckAvailabilityRequest
{
    [Required]
    public string Location { get; set; } = string.Empty;

    [Required]
    [Range(1, 100)]
    public int NumberOfGuests { get; set; }

    [Required]
    public DateTime CheckIn { get; set; }

    [Required]
    public DateTime CheckOut { get; set; }
}

using System.Security.Claims;

using AvailabilityService.Domain;
using AvailabilityService.Infrastructure;

using Hotelier.Events;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AvailabilityService.Api;

[ApiController]
[Route("api/[controller]")]
public class AvailabilityController(
    AvailabilityDbContext db,
    IPublishEndpoint publisher,
    IReservationServiceClient reservationClient,
    ILogger<AvailabilityController> logger) : ControllerBase
{
    // -------------------------------------------------------
    // POST /api/availability   (1.6 – define availability period)
    // -------------------------------------------------------
    [Authorize(Roles = "Host")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAvailabilityRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        if (request.FromDate >= request.ToDate)
            return BadRequest(new { message = "FromDate must be before ToDate." });

        if (request.FromDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new { message = "Cannot create availability periods in the past." });

        var availability = new Availability
        {
            AccommodationId = request.AccommodationId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Price = request.Price,
            PriceType = request.PriceType,
            PriceModifiers = request.PriceModifiers,
            CreatedBy = hostId.Value.ToString()
        };

        db.Availabilities.Add(availability);
        await db.SaveChangesAsync();

        await PublishUpdate(availability);

        logger.LogInformation(
            "Availability {Id} created for accommodation {AccommodationId} ({From}-{To})",
            availability.Id, availability.AccommodationId, availability.FromDate, availability.ToDate);

        return CreatedAtAction(nameof(GetById), new { id = availability.Id }, MapResponse(availability));
    }

    // -------------------------------------------------------
    // PUT /api/availability/{id}   (1.6 – update period/pricing)
    // -------------------------------------------------------
    [Authorize(Roles = "Host")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAvailabilityRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        var availability = await db.Availabilities.FindAsync(id);
        if (availability is null) return NotFound();

        // Spec 1.6: prevent changes if reservations exist in the period
        var (hasReservations, reason) = await reservationClient
            .HasReservationsInPeriodAsync(availability.AccommodationId, availability.FromDate, availability.ToDate);

        if (hasReservations)
            return Conflict(new { message = reason });

        if (request.FromDate.HasValue) availability.FromDate = request.FromDate.Value;
        if (request.ToDate.HasValue) availability.ToDate = request.ToDate.Value;

        if (availability.FromDate >= availability.ToDate)
            return BadRequest(new { message = "FromDate must be before ToDate." });

        if (request.Price.HasValue) availability.Price = request.Price.Value;
        if (request.PriceType.HasValue) availability.PriceType = request.PriceType.Value;
        if (request.PriceModifiers is not null) availability.PriceModifiers = request.PriceModifiers;
        if (request.IsAvailable.HasValue) availability.IsAvailable = request.IsAvailable.Value;

        availability.ModifiedBy = hostId.Value.ToString();
        await db.SaveChangesAsync();

        await PublishUpdate(availability);

        logger.LogInformation("Availability {Id} updated", id);

        return Ok(MapResponse(availability));
    }

    // -------------------------------------------------------
    // DELETE /api/availability/{id}
    // -------------------------------------------------------
    [Authorize(Roles = "Host")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var hostId = GetUserId();
        if (hostId is null) return Unauthorized();

        var availability = await db.Availabilities.FindAsync(id);
        if (availability is null) return NotFound();

        // Spec 1.6: prevent changes if reservations exist
        var (hasReservations, reason) = await reservationClient
            .HasReservationsInPeriodAsync(availability.AccommodationId, availability.FromDate, availability.ToDate);

        if (hasReservations)
            return Conflict(new { message = reason });

        db.Availabilities.Remove(availability);
        await db.SaveChangesAsync();

        logger.LogInformation("Availability {Id} deleted", id);

        return NoContent();
    }

    // -------------------------------------------------------
    // GET /api/availability/{id}
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var availability = await db.Availabilities.FindAsync(id);
        if (availability is null) return NotFound();

        return Ok(MapResponse(availability));
    }

    // -------------------------------------------------------
    // GET /api/availability/accommodation/{accommodationId}
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("accommodation/{accommodationId:guid}")]
    public async Task<IActionResult> GetByAccommodation(Guid accommodationId, [FromQuery] bool availableOnly = false)
    {
        var query = db.Availabilities
            .Where(a => a.AccommodationId == accommodationId)
            .OrderBy(a => a.FromDate)
            .AsQueryable();

        if (availableOnly)
            query = query.Where(a => a.IsAvailable && a.ToDate > DateTime.UtcNow);

        var results = await query.ToListAsync();

        return Ok(results.Select(MapResponse));
    }

    // -------------------------------------------------------
    // GET /api/availability/internal/check   (service-to-service)
    // Check if an accommodation has availability for given dates.
    // Used by reservation-service to validate bookings.
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("internal/check")]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] Guid accommodationId,
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut)
    {
        var available = await db.Availabilities.AnyAsync(a =>
            a.AccommodationId == accommodationId
            && a.IsAvailable
            && a.FromDate <= checkIn
            && a.ToDate >= checkOut);

        // Also retrieve pricing for the period
        AvailabilityPriceInfo? priceInfo = null;
        if (available)
        {
            var window = await db.Availabilities.FirstOrDefaultAsync(a =>
                a.AccommodationId == accommodationId
                && a.IsAvailable
                && a.FromDate <= checkIn
                && a.ToDate >= checkOut);

            if (window is not null)
            {
                var nights = (checkOut.Date - checkIn.Date).Days;
                priceInfo = new AvailabilityPriceInfo
                {
                    PricePerNight = window.Price,
                    PriceType = window.PriceType.ToString(),
                    Nights = nights,
                    TotalPrice = window.Price * nights,
                    PriceModifiers = window.PriceModifiers
                };
            }
        }

        return Ok(new CheckAvailabilityResponse
        {
            IsAvailable = available,
            Price = priceInfo
        });
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------
    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private async Task PublishUpdate(Availability a)
    {
        await publisher.Publish(new AvailabilityUpdated
        {
            AvailabilityId = a.Id,
            AccommodationId = a.AccommodationId,
            FromDate = a.FromDate,
            ToDate = a.ToDate,
            Price = a.Price,
            PriceType = a.PriceType.ToString(),
            IsAvailable = a.IsAvailable
        });
    }

    private static AvailabilityResponse MapResponse(Availability a) => new()
    {
        Id = a.Id,
        AccommodationId = a.AccommodationId,
        FromDate = a.FromDate,
        ToDate = a.ToDate,
        Price = a.Price,
        PriceType = a.PriceType,
        PriceModifiers = a.PriceModifiers,
        IsAvailable = a.IsAvailable
    };
}

public class CheckAvailabilityResponse
{
    public bool IsAvailable { get; set; }
    public AvailabilityPriceInfo? Price { get; set; }
}

public class AvailabilityPriceInfo
{
    public decimal PricePerNight { get; set; }
    public string PriceType { get; set; } = string.Empty;
    public int Nights { get; set; }
    public decimal TotalPrice { get; set; }
    public Dictionary<string, decimal> PriceModifiers { get; set; } = new();
}

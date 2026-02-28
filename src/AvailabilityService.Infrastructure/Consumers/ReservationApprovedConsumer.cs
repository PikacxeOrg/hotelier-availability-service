using Hotelier.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvailabilityService.Infrastructure;

/// <summary>
/// When a reservation is approved, mark the dates as unavailable
/// by setting IsAvailable = false on overlapping availability windows.
/// </summary>
public class ReservationApprovedConsumer(
    AvailabilityDbContext db,
    ILogger<ReservationApprovedConsumer> logger)
    : IConsumer<ReservationApproved>
{
    public async Task Consume(ConsumeContext<ReservationApproved> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Reservation {ReservationId} approved – marking dates {From}-{To} unavailable for accommodation {AccommodationId}",
            msg.ReservationId, msg.FromDate, msg.ToDate, msg.AccommodationId);

        var overlapping = await db.Availabilities
            .Where(a => a.AccommodationId == msg.AccommodationId
                        && a.IsAvailable
                        && a.FromDate < msg.ToDate
                        && a.ToDate > msg.FromDate)
            .ToListAsync();

        foreach (var window in overlapping)
        {
            window.IsAvailable = false;
            window.ModifiedBy = "system:reservation-approved";
        }

        await db.SaveChangesAsync();
    }
}

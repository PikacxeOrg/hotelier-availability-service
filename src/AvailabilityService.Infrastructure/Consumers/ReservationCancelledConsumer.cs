using Hotelier.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvailabilityService.Infrastructure;

/// <summary>
/// When a reservation is cancelled, free the dates back up
/// by setting IsAvailable = true on the matching availability windows.
/// </summary>
public class ReservationCancelledConsumer(
    AvailabilityDbContext db,
    ILogger<ReservationCancelledConsumer> logger)
    : IConsumer<ReservationCancelled>
{
    public async Task Consume(ConsumeContext<ReservationCancelled> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Reservation {ReservationId} cancelled – freeing dates {From}-{To} for accommodation {AccommodationId}",
            msg.ReservationId, msg.FromDate, msg.ToDate, msg.AccommodationId);

        var overlapping = await db.Availabilities
            .Where(a => a.AccommodationId == msg.AccommodationId
                        && !a.IsAvailable
                        && a.FromDate < msg.ToDate
                        && a.ToDate > msg.FromDate)
            .ToListAsync();

        foreach (var window in overlapping)
        {
            window.IsAvailable = true;
            window.ModifiedBy = "system:reservation-cancelled";
        }

        await db.SaveChangesAsync();
    }
}

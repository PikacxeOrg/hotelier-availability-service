using Hotelier.Events;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvailabilityService.Infrastructure;

/// <summary>
/// When an accommodation is deleted, remove all its availability windows.
/// </summary>
public class AccommodationDeletedConsumer(
    AvailabilityDbContext db,
    ILogger<AccommodationDeletedConsumer> logger)
    : IConsumer<AccommodationDeleted>
{
    public async Task Consume(ConsumeContext<AccommodationDeleted> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Accommodation {AccommodationId} deleted – removing all availability windows",
            msg.AccommodationId);

        var windows = await db.Availabilities
            .Where(a => a.AccommodationId == msg.AccommodationId)
            .ToListAsync();

        if (windows.Count > 0)
        {
            db.Availabilities.RemoveRange(windows);
            await db.SaveChangesAsync();
            logger.LogInformation("Removed {Count} availability windows", windows.Count);
        }
    }
}

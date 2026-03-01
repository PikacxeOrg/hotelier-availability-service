namespace AvailabilityService.Domain;

/// <summary>
/// Checks reservation-service to see if reservations exist
/// in a given accommodation's date range.
/// </summary>
public interface IReservationServiceClient
{
    Task<(bool HasReservations, string? Reason)> HasReservationsInPeriodAsync(
        Guid accommodationId, DateOnly fromDate, DateOnly toDate);
}

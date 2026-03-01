using System.Net.Http.Json;
using System.Text.Json;

using AvailabilityService.Domain;

using Microsoft.Extensions.Logging;

namespace AvailabilityService.Infrastructure;

public class ReservationServiceClient(
    HttpClient httpClient,
    ILogger<ReservationServiceClient> logger)
    : IReservationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<(bool HasReservations, string? Reason)> HasReservationsInPeriodAsync(
        Guid accommodationId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var url = $"/api/reservations/internal/has-reservations" +
                      $"?accommodationId={accommodationId}" +
                      $"&fromDate={fromDate:O}" +
                      $"&toDate={toDate:O}";

            logger.LogDebug("Checking reservations: {Url}", url);

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<HasReservationsDto>(JsonOptions);
            return (body?.HasReservations ?? false, body?.Reason);
        }
        catch (Exception ex)
        {
            // Fail-closed: if we can't reach reservation-service, block the change
            logger.LogWarning(ex,
                "Could not reach reservation-service. Blocking availability change for safety.");
            return (true, "Cannot verify reservations at this time. Please try again later.");
        }
    }

    private sealed class HasReservationsDto
    {
        public bool HasReservations { get; set; }
        public int Count { get; set; }
        public string? Reason { get; set; }
    }
}

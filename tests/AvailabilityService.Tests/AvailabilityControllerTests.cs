using System.Security.Claims;

using AvailabilityService.Api;
using AvailabilityService.Domain;
using AvailabilityService.Infrastructure;

using FluentAssertions;

using Hotelier.Events;

using MassTransit;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace AvailabilityService.Tests;

public class AvailabilityControllerTests : IDisposable
{
    private readonly AvailabilityDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly Mock<IReservationServiceClient> _reservationClientMock;
    private readonly AvailabilityController _sut;
    private readonly Guid _hostId = Guid.NewGuid();
    private readonly Guid _accommodationId = Guid.NewGuid();

    public AvailabilityControllerTests()
    {
        var options = new DbContextOptionsBuilder<AvailabilityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AvailabilityDbContext(options);
        _publisherMock = new Mock<IPublishEndpoint>();
        _reservationClientMock = new Mock<IReservationServiceClient>();
        var logger = new Mock<ILogger<AvailabilityController>>();

        // Default: no reservations blocking changes
        _reservationClientMock
            .Setup(c => c.HasReservationsInPeriodAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync((false, (string?)null));

        _sut = new AvailabilityController(_db, _publisherMock.Object, _reservationClientMock.Object, logger.Object);
        SetAuthenticatedUser(_hostId);
    }

    public void Dispose() => _db.Dispose();

    private void SetAuthenticatedUser(Guid userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private Availability SeedAvailability(
        DateOnly? from = null, DateOnly? to = null,
        decimal price = 100m, PriceType priceType = PriceType.PerUnit,
        bool isAvailable = true)
    {
        var avail = new Availability
        {
            AccommodationId = _accommodationId,
            FromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            ToDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
            Price = price,
            PriceType = priceType,
            IsAvailable = isAvailable,
            CreatedBy = _hostId.ToString()
        };
        _db.Availabilities.Add(avail);
        _db.SaveChanges();
        return avail;
    }

    // ============================================================
    // POST /api/availability
    // ============================================================

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated()
    {
        var request = new CreateAvailabilityRequest
        {
            AccommodationId = _accommodationId,
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            ToDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            Price = 150m,
            PriceType = PriceType.PerGuest
        };

        var result = await _sut.Create(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var body = created.Value.Should().BeOfType<AvailabilityResponse>().Subject;
        body.AccommodationId.Should().Be(_accommodationId);
        body.Price.Should().Be(150m);
        body.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Create_PublishesAvailabilityUpdatedEvent()
    {
        var request = new CreateAvailabilityRequest
        {
            AccommodationId = _accommodationId,
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            ToDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            Price = 80m,
            PriceType = PriceType.PerUnit
        };

        await _sut.Create(request);

        _publisherMock.Verify(p => p.Publish(
            It.Is<AvailabilityUpdated>(e =>
                e.AccommodationId == _accommodationId &&
                e.Price == 80m &&
                e.IsAvailable),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_FromDateAfterToDate_ReturnsBadRequest()
    {
        var request = new CreateAvailabilityRequest
        {
            AccommodationId = _accommodationId,
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            ToDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            Price = 100m,
            PriceType = PriceType.PerUnit
        };

        var result = await _sut.Create(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithPriceModifiers_PersistsModifiers()
    {
        var request = new CreateAvailabilityRequest
        {
            AccommodationId = _accommodationId,
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            ToDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            Price = 100m,
            PriceType = PriceType.PerUnit,
            PriceModifiers = new Dictionary<string, decimal>
            {
                ["Weekend"] = 1.2m,
                ["Summer"] = 1.5m
            }
        };

        var result = await _sut.Create(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var body = created.Value.Should().BeOfType<AvailabilityResponse>().Subject;
        body.PriceModifiers.Should().ContainKey("Weekend");
        body.PriceModifiers["Weekend"].Should().Be(1.2m);
    }

    // ============================================================
    // PUT /api/availability/{id}
    // ============================================================

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        var avail = SeedAvailability();
        var request = new UpdateAvailabilityRequest { Price = 200m };

        var result = await _sut.Update(avail.Id, request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<AvailabilityResponse>().Subject;
        body.Price.Should().Be(200m);
    }

    [Fact]
    public async Task Update_ExistingReservations_ReturnsConflict()
    {
        var avail = SeedAvailability();

        _reservationClientMock
            .Setup(c => c.HasReservationsInPeriodAsync(
                _accommodationId, avail.FromDate, avail.ToDate))
            .ReturnsAsync((true, "Cannot modify: 1 reservation(s) exist."));

        var request = new UpdateAvailabilityRequest { Price = 200m };
        var result = await _sut.Update(avail.Id, request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        var result = await _sut.Update(Guid.NewGuid(), new UpdateAvailabilityRequest { Price = 50m });

        result.Should().BeOfType<NotFoundResult>();
    }

    // ============================================================
    // DELETE /api/availability/{id}
    // ============================================================

    [Fact]
    public async Task Delete_NoReservations_ReturnsNoContent()
    {
        var avail = SeedAvailability();

        var result = await _sut.Delete(avail.Id);

        result.Should().BeOfType<NoContentResult>();
        _db.Availabilities.Find(avail.Id).Should().BeNull();
    }

    [Fact]
    public async Task Delete_HasReservations_ReturnsConflict()
    {
        var avail = SeedAvailability();

        _reservationClientMock
            .Setup(c => c.HasReservationsInPeriodAsync(
                _accommodationId, avail.FromDate, avail.ToDate))
            .ReturnsAsync((true, "Cannot modify: 2 reservation(s) exist."));

        var result = await _sut.Delete(avail.Id);

        result.Should().BeOfType<ConflictObjectResult>();
        _db.Availabilities.Find(avail.Id).Should().NotBeNull();
    }

    // ============================================================
    // GET /api/availability/{id}
    // ============================================================

    [Fact]
    public async Task GetById_Exists_ReturnsOk()
    {
        var avail = SeedAvailability();

        var result = await _sut.GetById(avail.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<AvailabilityResponse>().Subject;
        body.Id.Should().Be(avail.Id);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var result = await _sut.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    // ============================================================
    // GET /api/availability/accommodation/{accommodationId}
    // ============================================================

    [Fact]
    public async Task GetByAccommodation_ReturnsAll()
    {
        SeedAvailability();
        SeedAvailability(from: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), to: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40));

        var result = await _sut.GetByAccommodation(_accommodationId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AvailabilityResponse>>().Subject;
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByAccommodation_AvailableOnly_FiltersUnavailable()
    {
        SeedAvailability(isAvailable: true);
        SeedAvailability(from: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), to: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40), isAvailable: false);

        var result = await _sut.GetByAccommodation(_accommodationId, availableOnly: true);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<IEnumerable<AvailabilityResponse>>().Subject;
        list.Should().HaveCount(1);
    }

    // ============================================================
    // GET /api/availability/internal/check
    // ============================================================

    [Fact]
    public async Task CheckAvailability_Available_ReturnsTrueWithPrice()
    {
        SeedAvailability(
            from: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            to: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20),
            price: 100m);

        var result = await _sut.CheckAvailability(
            _accommodationId,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<CheckAvailabilityResponse>().Subject;
        body.IsAvailable.Should().BeTrue();
        body.Price.Should().NotBeNull();
        body.Price!.PricePerNight.Should().Be(100m);
        body.Price.Nights.Should().Be(5);
    }

    [Fact]
    public async Task CheckAvailability_NotAvailable_ReturnsFalse()
    {
        // No availability windows seeded
        var result = await _sut.CheckAvailability(
            _accommodationId,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<CheckAvailabilityResponse>().Subject;
        body.IsAvailable.Should().BeFalse();
        body.Price.Should().BeNull();
    }
}

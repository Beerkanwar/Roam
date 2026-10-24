using Roam.Application.TourRequests;
using Roam.Domain.TourRequests;
using Xunit;

namespace Roam.Application.Tests.TourRequests;

public class CreateTourRequestDtoTests
{
    [Fact]
    public void Dto_Initialization_ShouldSetDefaults()
    {
        var dto = new CreateTourRequestDto();
        Assert.Equal(string.Empty, dto.PlaceId);
        Assert.Equal(string.Empty, dto.RequesterId);
        Assert.Equal(string.Empty, dto.Description);
        Assert.Equal(VisibilityMode.Private, dto.Visibility);
        Assert.Equal(SchedulingMode.Immediate, dto.Mode);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(90, 180, true)]
    [InlineData(-90, -180, true)]
    [InlineData(45.5, -73.5, true)]
    [InlineData(90.1, 0, false)]
    [InlineData(-90.1, 0, false)]
    [InlineData(0, 180.1, false)]
    [InlineData(0, -180.1, false)]
    [InlineData(double.NaN, 0, false)]
    [InlineData(0, double.NaN, false)]
    public void CoordinateDto_Validation_ValidatesRangeCorrectly(double lat, double lng, bool expectedValid)
    {
        var dto = new CreateTourRequestByCoordinatesDto
        {
            Latitude = lat,
            Longitude = lng,
            RequesterId = "user-123",
            Description = "Test coordinates"
        };

        bool isValid = dto.Validate(out var error);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.NotNull(error);
        }
    }

    [Fact]
    public void CreateTourRequestDto_CustomProperties_SetProperly()
    {
        var dto = new CreateTourRequestDto
        {
            PlaceId = "place-99",
            RequesterId = "user-abc",
            Description = "Visiting historical museum",
            Visibility = VisibilityMode.Private,
            Mode = SchedulingMode.Scheduled
        };

        Assert.Equal("place-99", dto.PlaceId);
        Assert.Equal("user-abc", dto.RequesterId);
        Assert.Equal("Visiting historical museum", dto.Description);
        Assert.Equal(VisibilityMode.Private, dto.Visibility);
        Assert.Equal(SchedulingMode.Scheduled, dto.Mode);
    }
}

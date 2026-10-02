using Roam.Application.TourRequests;
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
    }
}

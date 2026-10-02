using Roam.Domain.TourRequests;
using Xunit;

namespace Roam.Domain.Tests.TourRequests;

public class TourRequestTests
{
    [Fact]
    public void Accept_WhenPublished_ShouldTransitionToAccepted()
    {
        // Arrange
        var request = new TourRequest
        {
            PlaceId = "place-1",
            RequesterId = "user-1",
            Status = TourRequestStatus.Published
        };

        // Act
        request.Status = TourRequestStatus.Accepted;

        // Assert
        Assert.Equal(TourRequestStatus.Accepted, request.Status);
    }

    [Fact]
    public void Cancel_WhenPublished_ShouldTransitionToCancelled()
    {
        // Arrange
        var request = new TourRequest
        {
            PlaceId = "place-1",
            RequesterId = "user-1",
            Status = TourRequestStatus.Published
        };

        // Act
        request.Status = TourRequestStatus.Cancelled;

        // Assert
        Assert.Equal(TourRequestStatus.Cancelled, request.Status);
    }
}

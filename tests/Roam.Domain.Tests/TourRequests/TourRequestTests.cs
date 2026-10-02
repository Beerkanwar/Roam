using Roam.Domain.TourRequests;
using Xunit;

namespace Roam.Domain.Tests.TourRequests;

public class TourRequestTests
{
    [Fact]
    public void Accept_WhenPending_ShouldTransitionToAccepted()
    {
        // Arrange
        var request = new TourRequest
        {
            PlaceId = "place-1",
            RequesterId = "user-1",
            Status = TourRequestStatus.Pending
        };

        // Act
        request.Status = TourRequestStatus.Accepted;

        // Assert
        Assert.Equal(TourRequestStatus.Accepted, request.Status);
    }

    [Fact]
    public void Cancel_WhenPending_ShouldTransitionToCancelled()
    {
        // Arrange
        var request = new TourRequest
        {
            PlaceId = "place-1",
            RequesterId = "user-1",
            Status = TourRequestStatus.Pending
        };

        // Act
        request.Status = TourRequestStatus.Cancelled;

        // Assert
        Assert.Equal(TourRequestStatus.Cancelled, request.Status);
    }
}

using System;
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
        request.TransitionTo(TourRequestStatus.Accepted);

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
        request.TransitionTo(TourRequestStatus.Cancelled);

        // Assert
        Assert.Equal(TourRequestStatus.Cancelled, request.Status);
    }

    [Fact]
    public void TransitionTo_WhenIllegalTransition_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new TourRequest
        {
            PlaceId = "place-1",
            RequesterId = "user-1",
            Status = TourRequestStatus.Draft
        };

        // Act & Assert
        Assert.False(request.CanTransitionTo(TourRequestStatus.Completed));
        var ex = Assert.Throws<InvalidOperationException>(() => request.TransitionTo(TourRequestStatus.Completed));
        Assert.Contains("Cannot transition from Draft to Completed", ex.Message);
    }
}

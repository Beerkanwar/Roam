using Roam.Domain.TourRequests;
using Xunit;

namespace Roam.Domain.Tests.TourRequests;

public class TourRequestReleaseVolunteerTests
{
    [Theory]
    [InlineData(TourRequestStatus.VolunteerInterested)]
    [InlineData(TourRequestStatus.Accepted)]
    [InlineData(TourRequestStatus.Confirmed)]
    public void ReleaseVolunteer_ReturnsRequestToDiscovery(TourRequestStatus status)
    {
        var request = new TourRequest
        {
            Status = status,
            AssignedVolunteerId = "volunteer-1",
            RequestedStartTime = DateTime.UtcNow.AddDays(1)
        };

        request.ReleaseVolunteer();

        Assert.Equal(TourRequestStatus.Published, request.Status);
        Assert.Null(request.AssignedVolunteerId);
        Assert.Null(request.RequestedStartTime);
    }

    [Theory]
    [InlineData(TourRequestStatus.Published)]
    [InlineData(TourRequestStatus.Active)]
    [InlineData(TourRequestStatus.Completed)]
    [InlineData(TourRequestStatus.Cancelled)]
    [InlineData(TourRequestStatus.Expired)]
    public void ReleaseVolunteer_IsRejected_OutsidePreStartStates(TourRequestStatus status)
    {
        var request = new TourRequest { Status = status, AssignedVolunteerId = "volunteer-1" };

        Assert.False(request.CanReleaseVolunteer);
        Assert.Throws<InvalidOperationException>(() => request.ReleaseVolunteer());
        Assert.Equal(status, request.Status);
    }

    [Fact]
    public void ReleaseVolunteer_IsRejected_WhenNoVolunteerAssigned()
    {
        var request = new TourRequest { Status = TourRequestStatus.Accepted };

        Assert.False(request.CanReleaseVolunteer);
        Assert.Throws<InvalidOperationException>(() => request.ReleaseVolunteer());
    }
}

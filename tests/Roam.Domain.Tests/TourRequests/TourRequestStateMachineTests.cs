using Roam.Domain.TourRequests;
using Xunit;

namespace Roam.Domain.Tests.TourRequests;

public class TourRequestStateMachineTests
{
    [Fact]
    public void Draft_CanTransitionTo_Published()
    {
        var request = new TourRequest { Status = TourRequestStatus.Draft };
        request.TransitionTo(TourRequestStatus.Published);
        Assert.Equal(TourRequestStatus.Published, request.Status);
    }

    [Fact]
    public void Draft_CannotTransitionTo_Accepted()
    {
        var request = new TourRequest { Status = TourRequestStatus.Draft };
        
        var ex = Assert.Throws<InvalidOperationException>(() => request.TransitionTo(TourRequestStatus.Accepted));
        Assert.Contains("Cannot transition from Draft to Accepted", ex.Message);
    }

    [Theory]
    [InlineData(TourRequestStatus.Published, TourRequestStatus.VolunteerInterested)]
    [InlineData(TourRequestStatus.Matching, TourRequestStatus.VolunteerInterested)]
    [InlineData(TourRequestStatus.Published, TourRequestStatus.Accepted)]
    [InlineData(TourRequestStatus.VolunteerInterested, TourRequestStatus.Accepted)]
    [InlineData(TourRequestStatus.Accepted, TourRequestStatus.Confirmed)]
    [InlineData(TourRequestStatus.Confirmed, TourRequestStatus.Active)]
    [InlineData(TourRequestStatus.Active, TourRequestStatus.Completed)]
    public void ValidTransitions_ShouldSucceed(TourRequestStatus current, TourRequestStatus target)
    {
        var request = new TourRequest { Status = current };
        request.TransitionTo(target);
        Assert.Equal(target, request.Status);
    }

    [Theory]
    [InlineData(TourRequestStatus.Draft, TourRequestStatus.Completed)]
    [InlineData(TourRequestStatus.Published, TourRequestStatus.Completed)]
    [InlineData(TourRequestStatus.Matching, TourRequestStatus.Completed)]
    [InlineData(TourRequestStatus.Confirmed, TourRequestStatus.Completed)]
    [InlineData(TourRequestStatus.Completed, TourRequestStatus.Active)]
    [InlineData(TourRequestStatus.Cancelled, TourRequestStatus.Accepted)]
    public void InvalidTransitions_ShouldThrow(TourRequestStatus current, TourRequestStatus target)
    {
        var request = new TourRequest { Status = current };
        Assert.Throws<InvalidOperationException>(() => request.TransitionTo(target));
    }
}

using System;
using Roam.Domain.Payments;
using Xunit;

namespace Roam.Domain.Tests.Payments;

public class PaymentDomainTests
{
    [Fact]
    public void TipTransaction_MarkCompleted_FromPending_Succeeds()
    {
        var tip = new TipTransaction("sender1", "receiver1", 10.00m, "USD");
        Assert.Equal(PaymentStatus.Pending, tip.Status);

        tip.MarkCompleted();

        Assert.Equal(PaymentStatus.Completed, tip.Status);
    }

    [Fact]
    public void TipTransaction_MarkCompleted_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var tip = new TipTransaction("sender1", "receiver1", 10.00m, "USD");
        tip.MarkCompleted();

        var ex = Assert.Throws<InvalidOperationException>(() => tip.MarkCompleted());
        Assert.Contains("Cannot complete tip transaction in Completed status", ex.Message);
    }

    [Fact]
    public void TipTransaction_MarkFailed_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var tip = new TipTransaction("sender1", "receiver1", 10.00m, "USD");
        tip.MarkCompleted();

        var ex = Assert.Throws<InvalidOperationException>(() => tip.MarkFailed());
        Assert.Contains("Cannot fail tip transaction in Completed status", ex.Message);
    }

    [Fact]
    public void TipTransaction_MarkFailed_FromPending_Succeeds()
    {
        var tip = new TipTransaction("sender1", "receiver1", 10.00m, "USD");
        tip.MarkFailed();

        Assert.Equal(PaymentStatus.Failed, tip.Status);
    }

    [Fact]
    public void Donation_MarkCompleted_FromPending_Succeeds()
    {
        var donation = new Donation(25.00m, "USD", "donor1");
        Assert.Equal(PaymentStatus.Pending, donation.Status);

        donation.MarkCompleted();

        Assert.Equal(PaymentStatus.Completed, donation.Status);
    }

    [Fact]
    public void Donation_MarkCompleted_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var donation = new Donation(25.00m, "USD", "donor1");
        donation.MarkCompleted();

        var ex = Assert.Throws<InvalidOperationException>(() => donation.MarkCompleted());
        Assert.Contains("Cannot complete donation in Completed status", ex.Message);
    }

    [Fact]
    public void Donation_MarkFailed_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        var donation = new Donation(25.00m, "USD", "donor1");
        donation.MarkCompleted();

        var ex = Assert.Throws<InvalidOperationException>(() => donation.MarkFailed());
        Assert.Contains("Cannot fail donation in Completed status", ex.Message);
    }
}


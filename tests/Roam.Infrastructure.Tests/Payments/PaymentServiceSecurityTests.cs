using Roam.Infrastructure.Payments;
using Xunit;

namespace Roam.Infrastructure.Tests.Payments;

/// <summary>
/// Regression tests for payment-integrity fixes. The service is constructed without a DbContext,
/// so these also prove the checks run before any database access.
/// </summary>
public class PaymentServiceSecurityTests
{
    private readonly PaymentService _service = new(null!);

    [Theory]
    [InlineData("sender-1")]   // the tipper marking their own tip as paid
    [InlineData("receiver-1")] // the volunteer marking an unpaid tip as received
    [InlineData("unrelated-user")]
    public async Task SettleTipAsync_RejectsNonAdminCallers_IncludingTipParticipants(string callerId)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SettleTipAsync("tip-1", callerId, isAdmin: false, succeeded: true));
    }

    [Theory]
    [InlineData(10.001)]
    [InlineData(0.005)]
    public async Task SubmitTipAsync_RejectsSubCentAmounts(decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitTipAsync("sender-1", "receiver-1", amount, "USD", null));

        Assert.Equal("amount", ex.ParamName);
    }

    [Fact]
    public async Task SubmitDonationAsync_RejectsSubCentAmounts()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitDonationAsync(5.555m, "USD", "donor-1"));

        Assert.Equal("amount", ex.ParamName);
    }

    [Fact]
    public async Task SubmitTipAsync_RejectsOversizedIdempotencyKey()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitTipAsync("sender-1", "receiver-1", 5m, "USD", null, new string('k', 129)));

        Assert.Equal("idempotencyKey", ex.ParamName);
    }

    [Fact]
    public async Task SubmitDonationAsync_RejectsOversizedIdempotencyKey()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitDonationAsync(5m, "USD", "donor-1", new string('k', 129)));

        Assert.Equal("idempotencyKey", ex.ParamName);
    }
}

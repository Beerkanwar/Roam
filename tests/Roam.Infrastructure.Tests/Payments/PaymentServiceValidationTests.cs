using Roam.Infrastructure.Payments;
using Xunit;

namespace Roam.Infrastructure.Tests.Payments;

public class PaymentServiceValidationTests
{
    private readonly PaymentService _service = new(null!);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task SubmitTipAsync_ThrowsArgumentException_WhenAmountNotPositive(decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitTipAsync("sender-1", "receiver-1", amount, "USD", null));

        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XYZ")]
    [InlineData("BTC")]
    public async Task SubmitTipAsync_ThrowsArgumentException_WhenCurrencyUnsupported(string currency)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitTipAsync("sender-1", "receiver-1", 10.0m, currency, null));

        Assert.Equal("currency", ex.ParamName);
    }

    [Fact]
    public async Task SubmitTipAsync_ThrowsInvalidOperationException_WhenSenderTipsThemselves()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitTipAsync("user-self", "user-self", 10.0m, "USD", null));

        Assert.Contains("cannot tip themselves", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task SubmitDonationAsync_ThrowsArgumentException_WhenAmountNotPositive(decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitDonationAsync(amount, "USD", "donor-1"));

        Assert.Equal("amount", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("INVALID")]
    public async Task SubmitDonationAsync_ThrowsArgumentException_WhenCurrencyUnsupported(string currency)
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SubmitDonationAsync(5.0m, currency, "donor-1"));

        Assert.Equal("currency", ex.ParamName);
    }
}

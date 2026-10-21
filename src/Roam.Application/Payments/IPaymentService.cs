using System.Threading.Tasks;
using Roam.Domain.Payments;

namespace Roam.Application.Payments;

public interface IPaymentService
{
    Task<TipTransaction> SubmitTipAsync(string senderId, string receiverId, decimal amount, string currency, string? tourSessionId, string? idempotencyKey = null);
    Task<Donation> SubmitDonationAsync(decimal amount, string currency, string? donorId, string? idempotencyKey = null);
}

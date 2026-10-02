using System;
using System.Threading.Tasks;
using Roam.Domain.Payments;

namespace Roam.Application.Payments;

public interface IPaymentService
{
    Task<TipTransaction> SubmitTipAsync(Guid senderId, Guid receiverId, decimal amount, string currency, Guid? tourSessionId);
    Task<Donation> SubmitDonationAsync(decimal amount, string currency, Guid? donorId);
}

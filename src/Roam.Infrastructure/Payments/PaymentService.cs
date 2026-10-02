using System;
using System.Threading.Tasks;
using Roam.Application.Payments;
using Roam.Domain.Payments;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.Payments;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TipTransaction> SubmitTipAsync(Guid senderId, Guid receiverId, decimal amount, string currency, Guid? tourSessionId)
    {
        var tip = new TipTransaction(senderId, receiverId, amount, currency, tourSessionId);
        
        // Stubbed real-world processing
        tip.MarkCompleted();
        
        _context.TipTransactions.Add(tip);
        await _context.SaveChangesAsync();
        
        return tip;
    }

    public async Task<Donation> SubmitDonationAsync(decimal amount, string currency, Guid? donorId)
    {
        var donation = new Donation(amount, currency, donorId);
        
        // Stubbed real-world processing
        donation.MarkCompleted();
        
        _context.Donations.Add(donation);
        await _context.SaveChangesAsync();
        
        return donation;
    }
}

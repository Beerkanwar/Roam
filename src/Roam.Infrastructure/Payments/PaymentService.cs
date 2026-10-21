using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Roam.Application.Payments;
using Roam.Domain.Payments;
using Roam.Infrastructure.Persistence;

namespace Roam.Infrastructure.Payments;

public class PaymentService : IPaymentService
{
    private static readonly HashSet<string> AllowedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "CAD", "AUD"
    };

    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TipTransaction> SubmitTipAsync(string senderId, string receiverId, decimal amount, string currency, string? tourSessionId, string? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Tip amount must be greater than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency) || !AllowedCurrencies.Contains(currency))
            throw new ArgumentException($"Currency '{currency}' is not supported.", nameof(currency));

        if (senderId == receiverId)
            throw new InvalidOperationException("Sender cannot tip themselves.");

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _context.TipTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
            if (existing != null)
                return existing;
        }

        var tip = new TipTransaction(senderId, receiverId, amount, currency.ToUpperInvariant(), tourSessionId, idempotencyKey);
        
        // In real PSP flow: create payment intent, here we record completed transaction
        tip.MarkCompleted();
        
        _context.TipTransactions.Add(tip);
        await _context.SaveChangesAsync();
        
        return tip;
    }

    public async Task<Donation> SubmitDonationAsync(decimal amount, string currency, string? donorId, string? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Donation amount must be greater than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency) || !AllowedCurrencies.Contains(currency))
            throw new ArgumentException($"Currency '{currency}' is not supported.", nameof(currency));

        // Idempotency check
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _context.Donations
                .FirstOrDefaultAsync(d => d.IdempotencyKey == idempotencyKey);
            if (existing != null)
                return existing;
        }

        var donation = new Donation(amount, currency.ToUpperInvariant(), donorId, idempotencyKey);
        
        donation.MarkCompleted();
        
        _context.Donations.Add(donation);
        await _context.SaveChangesAsync();
        
        return donation;
    }
}

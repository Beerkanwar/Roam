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

        // 1. Verify receiver exists, is an approved volunteer, and has tipping enabled
        var receiver = await _context.Users
            .Include(u => u.VolunteerSettings)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == receiverId);

        if (receiver == null)
            throw new ArgumentException("Recipient user not found.", nameof(receiverId));

        if (receiver.VolunteerStatus != Domain.Users.VolunteerStatus.Approved)
            throw new InvalidOperationException("Tips can only be sent to approved volunteers.");

        if (receiver.VolunteerSettings?.TipEnabled != true)
            throw new InvalidOperationException("The volunteer does not have tipping enabled.");

        // 2. If tourSessionId is provided, verify both participants were in that session
        if (!string.IsNullOrEmpty(tourSessionId))
        {
            var session = await _context.TourSessions
                .Include(s => s.Participants)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == tourSessionId);

            if (session == null)
                throw new ArgumentException("Specified tour session not found.", nameof(tourSessionId));

            bool senderParticipated = session.HostVolunteerId == senderId || session.Participants.Any(p => p.UserId == senderId);
            bool receiverParticipated = session.HostVolunteerId == receiverId || session.Participants.Any(p => p.UserId == receiverId);

            if (!senderParticipated || !receiverParticipated)
                throw new InvalidOperationException("Both users must be participants in the specified tour session.");
        }

        // 3. Scoped idempotency check (SenderId + IdempotencyKey)
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _context.TipTransactions
                .FirstOrDefaultAsync(t => t.SenderId == senderId && t.IdempotencyKey == idempotencyKey);
            if (existing != null)
                return existing;
        }

        var tip = new TipTransaction(senderId, receiverId, amount, currency.ToUpperInvariant(), tourSessionId, idempotencyKey);
        
        // Transaction remains in Pending status until external PSP webhook confirms settlement
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

        // Idempotency check scoped to donorId
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _context.Donations
                .FirstOrDefaultAsync(d => d.DonorId == donorId && d.IdempotencyKey == idempotencyKey);
            if (existing != null)
                return existing;
        }

        var donation = new Donation(amount, currency.ToUpperInvariant(), donorId, idempotencyKey);
        
        // Transaction remains in Pending status until external PSP webhook confirms settlement
        _context.Donations.Add(donation);
        await _context.SaveChangesAsync();
        
        return donation;
    }
}

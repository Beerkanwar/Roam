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

    private const int MaxIdempotencyKeyLength = 128;

    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TipTransaction> SubmitTipAsync(string senderId, string receiverId, decimal amount, string currency, string? tourSessionId, string? idempotencyKey = null)
    {
        if (amount <= 0 || amount > 1000m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Tip amount must be between 0.01 and 1000.00 with at most two decimal places.", nameof(amount));

        if (idempotencyKey?.Length > MaxIdempotencyKeyLength)
            throw new ArgumentException($"Idempotency key cannot exceed {MaxIdempotencyKeyLength} characters.", nameof(idempotencyKey));

        if (string.IsNullOrWhiteSpace(currency) || !AllowedCurrencies.Contains(currency))
            throw new ArgumentException($"Currency '{currency}' is not supported.", nameof(currency));

        if (senderId == receiverId)
            throw new InvalidOperationException("Sender cannot tip themselves.");

        // Check if sender or receiver has blocked the other
        var isBlocked = await _context.UserBlocks.AnyAsync(b =>
            (b.BlockerId == senderId && b.BlockedId == receiverId) ||
            (b.BlockerId == receiverId && b.BlockedId == senderId));

        if (isBlocked)
            throw new InvalidOperationException("Cannot send tips to a blocked user.");

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

        // If receiver has a configured TipAccount, verify it is enabled
        if (Guid.TryParse(receiverId, out var receiverGuid))
        {
            var tipAccount = await _context.TipAccounts.FirstOrDefaultAsync(a => a.UserId == receiverGuid);
            if (tipAccount != null && !tipAccount.IsEnabled)
            {
                throw new InvalidOperationException("The volunteer's tip account is disabled.");
            }
        }

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
            {
                if (existing.Amount != amount ||
                    !string.Equals(existing.Currency, currency, StringComparison.OrdinalIgnoreCase) ||
                    existing.ReceiverId != receiverId ||
                    existing.TourSessionId != tourSessionId)
                {
                    throw new InvalidOperationException("Idempotency key was previously used with different tip request parameters.");
                }

                return existing;
            }
        }

        var tip = new TipTransaction(senderId, receiverId, amount, currency.ToUpperInvariant(), tourSessionId, idempotencyKey);
        
        // Transaction remains in Pending status until external PSP webhook confirms settlement
        _context.TipTransactions.Add(tip);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            // A concurrent request with the same idempotency key won the unique-index race.
            _context.Entry(tip).State = EntityState.Detached;
            var winner = await _context.TipTransactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.SenderId == senderId && t.IdempotencyKey == idempotencyKey);
            if (winner == null) throw;
            if (winner.Amount != amount || winner.ReceiverId != receiverId || winner.TourSessionId != tourSessionId ||
                !string.Equals(winner.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Idempotency key was previously used with different tip request parameters.");
            }
            return winner;
        }

        return tip;
    }

    public async Task<Donation> SubmitDonationAsync(decimal amount, string currency, string? donorId, string? idempotencyKey = null)
    {
        if (amount <= 0 || amount > 1000m || decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Donation amount must be between 0.01 and 1000.00 with at most two decimal places.", nameof(amount));

        if (idempotencyKey?.Length > MaxIdempotencyKeyLength)
            throw new ArgumentException($"Idempotency key cannot exceed {MaxIdempotencyKeyLength} characters.", nameof(idempotencyKey));

        if (string.IsNullOrWhiteSpace(currency) || !AllowedCurrencies.Contains(currency))
            throw new ArgumentException($"Currency '{currency}' is not supported.", nameof(currency));

        // Idempotency check scoped to donorId
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _context.Donations
                .FirstOrDefaultAsync(d => d.DonorId == donorId && d.IdempotencyKey == idempotencyKey);
            if (existing != null)
            {
                if (existing.Amount != amount ||
                    !string.Equals(existing.Currency, currency, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Idempotency key was previously used with different donation request parameters.");
                }

                return existing;
            }
        }

        var donation = new Donation(amount, currency.ToUpperInvariant(), donorId, idempotencyKey);
        
        // Transaction remains in Pending status until external PSP webhook confirms settlement
        _context.Donations.Add(donation);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            // A concurrent request with the same idempotency key won the unique-index race.
            _context.Entry(donation).State = EntityState.Detached;
            var winner = await _context.Donations.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DonorId == donorId && d.IdempotencyKey == idempotencyKey);
            if (winner == null) throw;
            if (winner.Amount != amount || !string.Equals(winner.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Idempotency key was previously used with different donation request parameters.");
            }
            return winner;
        }

        return donation;
    }

    public async Task<TipTransaction> SettleTipAsync(string tipId, string callerId, bool isAdmin, bool succeeded, CancellationToken cancellationToken = default)
    {
        // Settlement records the payment provider's outcome. Letting the sender or receiver settle
        // would allow either party to mark an unpaid tip as Completed, so only administrators
        // (acting on PSP confirmation) may settle.
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only an administrator may settle tip transactions.");

        var tip = await _context.TipTransactions.FirstOrDefaultAsync(t => t.Id == tipId, cancellationToken);
        if (tip == null)
            throw new KeyNotFoundException($"Tip transaction '{tipId}' was not found.");

        if (tip.Status != PaymentStatus.Pending)
            throw new InvalidOperationException($"Cannot settle tip transaction with status '{tip.Status}'. Only Pending tips can be settled.");

        if (succeeded)
            tip.MarkCompleted();
        else
            tip.MarkFailed();

        await _context.SaveChangesAsync(cancellationToken);
        return tip;
    }
}

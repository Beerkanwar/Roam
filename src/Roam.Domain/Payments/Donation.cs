using System;

namespace Roam.Domain.Payments;

public class Donation
{
    public string Id { get; private set; }
    public string? DonorId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Donation() 
    { 
        Id = Guid.NewGuid().ToString();
        Currency = "USD"; 
    }

    public Donation(decimal amount, string currency, string? donorId = null, string? idempotencyKey = null)
    {
        Id = Guid.NewGuid().ToString();
        DonorId = donorId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = PaymentStatus.Completed;
    }
}

using System;

namespace Roam.Domain.Payments;

public class Donation
{
    public Guid Id { get; private set; }
    public Guid? DonorId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Donation() { Currency = "USD"; } // EF Core

    public Donation(decimal amount, string currency, Guid? donorId = null)
    {
        Id = Guid.NewGuid();
        DonorId = donorId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = PaymentStatus.Completed;
    }
}

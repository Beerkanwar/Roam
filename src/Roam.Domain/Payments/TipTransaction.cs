using System;

namespace Roam.Domain.Payments;

public class TipTransaction
{
    public Guid Id { get; private set; }
    public Guid SenderId { get; private set; }
    public Guid ReceiverId { get; private set; }
    public Guid? TourSessionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TipTransaction() { Currency = "USD"; } // EF Core

    public TipTransaction(Guid senderId, Guid receiverId, decimal amount, string currency, Guid? tourSessionId = null)
    {
        Id = Guid.NewGuid();
        SenderId = senderId;
        ReceiverId = receiverId;
        TourSessionId = tourSessionId;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        Status = PaymentStatus.Completed;
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
    }
}

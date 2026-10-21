using System;

namespace Roam.Domain.Payments;

public class TipTransaction
{
    public string Id { get; private set; }
    public string SenderId { get; private set; }
    public string ReceiverId { get; private set; }
    public string? TourSessionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private TipTransaction() 
    { 
        Id = Guid.NewGuid().ToString();
        SenderId = string.Empty;
        ReceiverId = string.Empty;
        Currency = "USD"; 
    }

    public TipTransaction(string senderId, string receiverId, decimal amount, string currency, string? tourSessionId = null, string? idempotencyKey = null)
    {
        Id = Guid.NewGuid().ToString();
        SenderId = senderId;
        ReceiverId = receiverId;
        TourSessionId = tourSessionId;
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

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
    }
}

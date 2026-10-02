using System;

namespace Roam.Domain.Payments;

public class TipAccount
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string StripeAccountId { get; private set; }
    public bool IsEnabled { get; private set; }

    private TipAccount() { StripeAccountId = string.Empty; } // EF Core

    public TipAccount(Guid userId, string stripeAccountId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        StripeAccountId = stripeAccountId;
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }
    
    public void Enable()
    {
        IsEnabled = true;
    }
}

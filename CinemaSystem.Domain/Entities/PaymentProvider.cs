using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class PaymentProvider
{
    public string PaymentProviderId { get; set; } = null!;

    public string ProviderName { get; set; } = null!;

    public string? ApiEndpoint { get; set; }

    public string ProviderStatus { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}

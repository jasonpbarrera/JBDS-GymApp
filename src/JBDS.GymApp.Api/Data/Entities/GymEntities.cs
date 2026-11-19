using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;

namespace JBDS.GymApp.Api.Data.Entities;

public sealed class MembershipPlan : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
    public int? MaxCheckInsPerMonth { get; set; }
    public bool IncludesClasses { get; set; } = true;
    public int? MaxClassBookingsPerWeek { get; set; }
    public string? StripePriceId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MemberSubscription> Subscriptions { get; set; } = new List<MemberSubscription>();
}

public sealed class Member : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? EmergencyContact { get; set; }
    public string QrCodeToken { get; set; } = $"GYM-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    public string BarcodeNumber { get; set; } = Random.Shared.NextInt64(100000000000, 999999999999).ToString();
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MemberSubscription> Subscriptions { get; set; } = new List<MemberSubscription>();
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public ICollection<ClassBooking> ClassBookings { get; set; } = new List<ClassBooking>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public sealed class MemberSubscription : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid MemberId { get; set; }
    public Guid MembershipPlanId { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndDateUtc { get; set; }
    public DateTime? NextBillingDateUtc { get; set; }
    public bool AutoRenew { get; set; } = true;
    public string? StripeSubscriptionId { get; set; }

    public Member? Member { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }
}

public sealed class CheckIn : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid MemberId { get; set; }
    public DateTime CheckedInAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedOutAtUtc { get; set; }
    public CheckInMethod Method { get; set; } = CheckInMethod.QrCode;
    public Guid? CheckedInByUserId { get; set; }
    public bool IsAllowed { get; set; } = true;
    public string? DenialReason { get; set; }
    public string? Notes { get; set; }

    public Member? Member { get; set; }
}

public sealed class GymClass : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Strength";
    public string TrainerName { get; set; } = string.Empty;
    public Guid? TrainerUserId { get; set; }
    public int DefaultCapacity { get; set; } = 20;
    public int DurationMinutes { get; set; } = 60;
    public string LocationRoom { get; set; } = "Main Studio";
    public bool IsActive { get; set; } = true;

    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}

public sealed class ClassSession : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid GymClassId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public int Capacity { get; set; }
    public string TrainerName { get; set; } = string.Empty;
    public string LocationRoom { get; set; } = "Main Studio";
    public bool IsCancelled { get; set; }

    public GymClass? GymClass { get; set; }
    public ICollection<ClassBooking> Bookings { get; set; } = new List<ClassBooking>();
}

public sealed class ClassBooking : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid ClassSessionId { get; set; }
    public Guid MemberId { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTime BookedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedInAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public ClassSession? ClassSession { get; set; }
    public Member? Member { get; set; }
}

public sealed class Invoice : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid MemberId { get; set; }
    public Guid? MemberSubscriptionId { get; set; }
    public string InvoiceNumber { get; set; } = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;
    public DateTime DueDateUtc { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? PaidAtUtc { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? StripeInvoiceId { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Member? Member { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public sealed class Payment : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid InvoiceId { get; set; }
    public Guid MemberId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentMethod Method { get; set; } = PaymentMethod.CardTerminal;
    public string? StripePaymentIntentId { get; set; }
    public string Status { get; set; } = "Succeeded";
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Invoice? Invoice { get; set; }
    public Member? Member { get; set; }
}

using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;

namespace JBDS.GymApp.Api.Data.Entities;

public sealed class PosProduct : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = "Drinks";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; } = 25;
    public bool IsActive { get; set; } = true;
}

public sealed class PosSale : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid? MemberId { get; set; }
    public string BuyerName { get; set; } = "Walk-In Guest";
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CardTerminal;
    public DateTime SoldAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? SoldByUserId { get; set; }
    public string ItemsSummary { get; set; } = string.Empty;

    public Member? Member { get; set; }
}

public sealed class BookableResource : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = "PersonalTrainer";
    public string? Description { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ResourceBooking : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public Guid BookableResourceId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? Notes { get; set; }

    public BookableResource? Resource { get; set; }
    public Member? Member { get; set; }
}

public sealed class ProspectLead : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Source { get; set; } = "Website";
    public string Stage { get; set; } = "New";
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? ConvertedMemberId { get; set; }
}

public sealed class ClubAnnouncement : IProductTenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public bool IsPinned { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByName { get; set; } = "Gym Staff";
}

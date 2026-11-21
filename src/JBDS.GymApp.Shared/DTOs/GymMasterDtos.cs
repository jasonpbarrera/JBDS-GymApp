using JBDS.GymApp.Shared.Enums;

namespace JBDS.GymApp.Shared.DTOs;

// Public Online Member Signup & Digital Waiver
public sealed record PublicJoinInfoDto(
    Guid TenantId,
    string TenantSlug,
    string TenantName,
    string PrimaryColor,
    string AccentColor,
    string WaiverVersion,
    string WaiverText,
    IReadOnlyList<MembershipPlanDto> Plans,
    string ThemePreset = "superior-crimson",
    string? HeroTagline = null,
    string? Phone = null,
    string? ContactEmail = null);

public sealed record PublicJoinRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Phone,
    string? EmergencyContact,
    Guid MembershipPlanId,
    string WaiverSignedName,
    bool WaiverAccepted);

public sealed record UpdateMemberProfileRequest(
    string? Phone,
    string? EmergencyContact,
    DateOnly? DateOfBirth);

public sealed record SignWaiverRequest(
    string SignedName,
    bool Accepted);

// Staff Point of Sale (POS / Pro Shop)
public sealed record PosProductDto(
    Guid Id,
    string Name,
    string Sku,
    string Category,
    decimal Price,
    int StockQuantity,
    bool IsActive);

public sealed record UpsertPosProductRequest(
    string Name,
    string Sku,
    string Category,
    decimal Price,
    int StockQuantity = 25,
    bool IsActive = true);

public sealed record PosSaleDto(
    Guid Id,
    Guid? MemberId,
    string BuyerName,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    DateTime SoldAtUtc,
    string ItemsSummary);

public sealed record CreatePosSaleRequest(
    Guid PosProductId,
    int Quantity = 1,
    Guid? MemberId = null,
    string? WalkInBuyerName = null,
    PaymentMethod PaymentMethod = PaymentMethod.CardTerminal,
    bool ChargeToMemberAccount = false);

// Personal Training & Resource Bookings (Trainers, Sauna, Recovery, Racks)
public sealed record BookableResourceDto(
    Guid Id,
    string Name,
    string ResourceType,
    string? Description,
    int DurationMinutes,
    decimal Price,
    bool IsActive);

public sealed record CreateBookableResourceRequest(
    string Name,
    string ResourceType = "PersonalTrainer",
    string? Description = null,
    int DurationMinutes = 60,
    decimal Price = 0m);

public sealed record ResourceBookingDto(
    Guid Id,
    Guid BookableResourceId,
    string ResourceName,
    string ResourceType,
    Guid MemberId,
    string MemberName,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    BookingStatus Status,
    string? Notes);

public sealed record CreateResourceBookingRequest(
    Guid BookableResourceId,
    DateTime StartTimeUtc,
    Guid? MemberId = null,
    string? Notes = null);

// Prospect / Lead CRM Pipeline
public sealed record ProspectLeadDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string Source,
    string Stage,
    string? Notes,
    DateTime CreatedAtUtc,
    Guid? ConvertedMemberId);

public sealed record CreateProspectLeadRequest(
    string FullName,
    string Email,
    string? Phone = null,
    string Source = "Website",
    string? Notes = null);

public sealed record UpdateProspectLeadStageRequest(
    string Stage,
    string? Notes = null);

// Club Announcements & Member Inbox
public sealed record ClubAnnouncementDto(
    Guid Id,
    string Title,
    string Body,
    string Category,
    bool IsPinned,
    DateTime PublishedAtUtc,
    string CreatedByName);

public sealed record CreateClubAnnouncementRequest(
    string Title,
    string Body,
    string Category = "General",
    bool IsPinned = false);

// Staff Reports Summary
public sealed record StaffReportsDto(
    int TotalMembers,
    int ActiveMembers,
    int PausedMembers,
    int CancelledMembers,
    int CheckInsLast7Days,
    int CheckInsLast30Days,
    int DoorUnlocksLast30Days,
    decimal MembershipRevenue30Days,
    decimal PosRevenue30Days,
    int OpenLeadsCount,
    int ConvertedLeadsCount);

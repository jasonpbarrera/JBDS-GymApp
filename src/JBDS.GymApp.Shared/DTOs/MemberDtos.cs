using JBDS.GymApp.Shared.Enums;

namespace JBDS.GymApp.Shared.DTOs;

public sealed record MembershipPlanDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    BillingInterval BillingInterval,
    int? MaxCheckInsPerMonth,
    bool IncludesClasses,
    int? MaxClassBookingsPerWeek,
    string? StripePriceId,
    bool IsActive,
    int ActiveMemberCount);

public sealed record UpsertMembershipPlanRequest(
    string Name,
    string? Description,
    decimal Price,
    string Currency = "USD",
    BillingInterval BillingInterval = BillingInterval.Monthly,
    int? MaxCheckInsPerMonth = null,
    bool IncludesClasses = true,
    int? MaxClassBookingsPerWeek = null,
    string? StripePriceId = null,
    bool IsActive = true);

public sealed record MemberDto(
    Guid Id,
    Guid? UserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    string? EmergencyContact,
    string QrCodeToken,
    string BarcodeNumber,
    MembershipStatus Status,
    string? ActivePlanName,
    Guid? ActivePlanId,
    DateTime? NextBillingDateUtc,
    DateTime JoinedAtUtc,
    int TotalCheckIns,
    string DoorPinCode = "000000",
    DateTime? WaiverSignedAtUtc = null);

public sealed record CreateMemberRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    string? EmergencyContact = null,
    Guid? MembershipPlanId = null);

public sealed record UpdateMemberRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    string? EmergencyContact,
    MembershipStatus Status);

public sealed record AssignMembershipRequest(
    Guid MembershipPlanId,
    bool AutoRenew = true,
    DateTime? StartDateUtc = null);

public sealed record QrPassDto(
    Guid MemberId,
    string FullName,
    string TenantName,
    string QrCodeToken,
    string BarcodeNumber,
    MembershipStatus Status,
    string? ActivePlanName,
    string DoorPinCode = "000000");

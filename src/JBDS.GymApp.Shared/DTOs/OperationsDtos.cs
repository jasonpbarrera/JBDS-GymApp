using JBDS.GymApp.Shared.Enums;

namespace JBDS.GymApp.Shared.DTOs;

public sealed record CheckInDto(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string MemberEmail,
    DateTime CheckedInAtUtc,
    DateTime? CheckedOutAtUtc,
    CheckInMethod Method,
    bool IsAllowed,
    string? DenialReason,
    string? Notes);

public sealed record PerformCheckInRequest(
    string? Code,
    Guid? MemberId = null,
    CheckInMethod Method = CheckInMethod.QrCode,
    string? Notes = null);

public sealed record CheckInResultDto(
    bool Allowed,
    string Message,
    CheckInDto? CheckIn,
    MemberDto? Member);

public sealed record GymClassDto(
    Guid Id,
    string Name,
    string? Description,
    string Category,
    string TrainerName,
    int DefaultCapacity,
    int DurationMinutes,
    string LocationRoom,
    bool IsActive);

public sealed record CreateGymClassRequest(
    string Name,
    string? Description,
    string Category,
    string TrainerName,
    int DefaultCapacity = 20,
    int DurationMinutes = 60,
    string LocationRoom = "Main Studio");

public sealed record ClassSessionDto(
    Guid Id,
    Guid GymClassId,
    string ClassName,
    string Category,
    string TrainerName,
    string LocationRoom,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    int Capacity,
    int BookedCount,
    bool IsCancelled,
    IReadOnlyList<ClassBookingDto> Bookings);

public sealed record CreateClassSessionRequest(
    Guid GymClassId,
    DateTime StartTimeUtc,
    int? DurationMinutes = null,
    int? Capacity = null,
    string? TrainerName = null,
    string? LocationRoom = null);

public sealed record ClassBookingDto(
    Guid Id,
    Guid ClassSessionId,
    Guid MemberId,
    string MemberName,
    BookingStatus Status,
    DateTime BookedAtUtc);

public sealed record BookClassRequest(Guid MemberId);

public sealed record InvoiceDto(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    InvoiceStatus Status,
    DateTime DueDateUtc,
    DateTime? PaidAtUtc,
    string Description,
    string? StripeInvoiceId);

public sealed record CreateInvoiceRequest(
    Guid MemberId,
    decimal Amount,
    string Description,
    DateTime? DueDateUtc = null,
    string Currency = "USD");

public sealed record RecordPaymentRequest(
    Guid InvoiceId,
    decimal Amount,
    PaymentMethod Method = PaymentMethod.CardTerminal,
    string? Notes = null);

public sealed record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    Guid MemberId,
    string MemberName,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    string Status,
    DateTime PaidAtUtc,
    string? Notes);

public sealed record StripeCheckoutRequest(
    Guid InvoiceId,
    string SuccessUrl,
    string CancelUrl);

public sealed record StripeCheckoutResponse(
    string CheckoutUrl,
    string SessionId,
    bool IsSimulated);

public sealed record DashboardSummaryDto(
    string TenantName,
    string ProductName,
    int TotalMembers,
    int ActiveMembers,
    int TodayCheckIns,
    int UpcomingSessionsCount,
    decimal MonthlyRevenue,
    decimal OutstandingBalance,
    IReadOnlyList<CheckInDto> RecentCheckIns,
    IReadOnlyList<ClassSessionDto> UpcomingSessions);

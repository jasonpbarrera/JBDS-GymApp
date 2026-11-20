using JBDS.GymApp.Shared.Enums;

namespace JBDS.GymApp.Shared.DTOs;

public sealed record DoorUnlockRequest(
    string DoorName = "Main Entrance",
    string? Notes = null);

public sealed record DoorAccessEventDto(
    Guid Id,
    Guid MemberId,
    string MemberName,
    string DoorName,
    DateTime TriggeredAtUtc,
    bool Granted,
    int UnlockDurationSeconds,
    bool IsSimulatedHardware,
    string? Reason);

public sealed record DoorUnlockResultDto(
    bool Granted,
    string DoorName,
    int UnlockDurationSeconds,
    bool IsSimulatedHardware,
    string Message,
    DateTime TriggeredAtUtc,
    CheckInDto? CheckIn,
    DoorAccessEventDto? AccessEvent);

public sealed record UpdateSelfMembershipRequest(
    string Action,
    Guid? NewPlanId = null,
    bool? AutoRenew = null);

public sealed record WorkoutLogDto(
    Guid Id,
    Guid MemberId,
    DateTime WorkoutDateUtc,
    string Title,
    string Category,
    int DurationMinutes,
    int? EstimatedCalories,
    string? ExercisesSummary,
    string? Notes);

public sealed record CreateWorkoutLogRequest(
    string Title,
    string Category = "Strength",
    int DurationMinutes = 45,
    int? EstimatedCalories = null,
    string? ExercisesSummary = null,
    string? Notes = null,
    DateTime? WorkoutDateUtc = null);

public sealed record MemberPortalOverviewDto(
    MemberDto Member,
    QrPassDto QrPass,
    string TenantName,
    bool AutoRenew,
    IReadOnlyList<MembershipPlanDto> AvailablePlans,
    IReadOnlyList<CheckInDto> RecentCheckIns,
    IReadOnlyList<DoorAccessEventDto> RecentDoorUnlocks,
    IReadOnlyList<ClassSessionDto> UpcomingSessions,
    IReadOnlyList<WorkoutLogDto> RecentWorkouts,
    IReadOnlyList<InvoiceDto> Invoices,
    int WorkoutsThisWeek,
    int MinutesThisWeek);

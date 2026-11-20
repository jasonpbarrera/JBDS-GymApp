using System.Net.Http.Headers;
using System.Net.Http.Json;
using JBDS.GymApp.Shared.DTOs;

namespace JBDS.GymApp.Client.Services;

public sealed class GymApiClient(HttpClient http, GymAuthStateProvider auth)
{
    private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }

        if (!string.IsNullOrWhiteSpace(auth.ActiveTenantSlug))
        {
            req.Headers.Add("X-Tenant-Slug", auth.ActiveTenantSlug);
        }

        req.Headers.Add("X-Product-Key", "gym-membership");

        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        return req;
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var req = CreateRequest(method, url, body);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API error ({(int)res.StatusCode}): {err}");
        }

        if (res.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
        return await res.Content.ReadFromJsonAsync<T>();
    }

    private async Task SendVoidAsync(HttpMethod method, string url, object? body = null)
    {
        using var req = CreateRequest(method, url, body);
        using var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API error ({(int)res.StatusCode}): {err}");
        }
    }

    // Auth & Branding
    public Task<TenantBrandingDto?> GetBrandingAsync() => SendAsync<TenantBrandingDto>(HttpMethod.Get, "api/auth/tenant-branding");
    public Task<AuthResponse?> LoginAsync(LoginRequest req) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", req);
    public Task<AuthResponse?> RegisterAsync(RegisterRequest req) => SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", req);

    // Dashboard
    public Task<DashboardSummaryDto?> GetDashboardAsync() => SendAsync<DashboardSummaryDto>(HttpMethod.Get, "api/dashboard/summary");

    // Plans
    public Task<List<MembershipPlanDto>?> GetPlansAsync() => SendAsync<List<MembershipPlanDto>>(HttpMethod.Get, "api/plans");
    public Task<MembershipPlanDto?> CreatePlanAsync(UpsertMembershipPlanRequest req) => SendAsync<MembershipPlanDto>(HttpMethod.Post, "api/plans", req);

    // Members
    public Task<List<MemberDto>?> GetMembersAsync(string? search = null) =>
        SendAsync<List<MemberDto>>(HttpMethod.Get, string.IsNullOrWhiteSpace(search) ? "api/members" : $"api/members?search={Uri.EscapeDataString(search)}");
    public Task<MemberDto?> CreateMemberAsync(CreateMemberRequest req) => SendAsync<MemberDto>(HttpMethod.Post, "api/members", req);
    public Task<MemberDto?> AssignPlanAsync(Guid memberId, AssignMembershipRequest req) => SendAsync<MemberDto>(HttpMethod.Post, $"api/members/{memberId}/assign-plan", req);
    public Task<QrPassDto?> GetQrPassAsync(Guid memberId) => SendAsync<QrPassDto>(HttpMethod.Get, $"api/members/{memberId}/qr-pass");

    // Check-Ins
    public Task<List<CheckInDto>?> GetCheckInsAsync() => SendAsync<List<CheckInDto>>(HttpMethod.Get, "api/checkins");
    public Task<CheckInResultDto?> ScanCheckInAsync(PerformCheckInRequest req) => SendAsync<CheckInResultDto>(HttpMethod.Post, "api/checkins/scan", req);

    // Classes & Sessions
    public Task<List<GymClassDto>?> GetClassesAsync() => SendAsync<List<GymClassDto>>(HttpMethod.Get, "api/classes");
    public Task<GymClassDto?> CreateClassAsync(CreateGymClassRequest req) => SendAsync<GymClassDto>(HttpMethod.Post, "api/classes", req);
    public Task<List<ClassSessionDto>?> GetSessionsAsync() => SendAsync<List<ClassSessionDto>>(HttpMethod.Get, "api/classes/sessions");
    public Task<ClassSessionDto?> CreateSessionAsync(CreateClassSessionRequest req) => SendAsync<ClassSessionDto>(HttpMethod.Post, "api/classes/sessions", req);
    public Task<ClassBookingDto?> BookSessionAsync(Guid sessionId, Guid memberId) => SendAsync<ClassBookingDto>(HttpMethod.Post, $"api/classes/sessions/{sessionId}/book", new BookClassRequest(memberId));
    public Task CancelBookingAsync(Guid bookingId) => SendVoidAsync(HttpMethod.Post, $"api/classes/bookings/{bookingId}/cancel");

    // Billing
    public Task<List<InvoiceDto>?> GetInvoicesAsync() => SendAsync<List<InvoiceDto>>(HttpMethod.Get, "api/billing/invoices");
    public Task<InvoiceDto?> CreateInvoiceAsync(CreateInvoiceRequest req) => SendAsync<InvoiceDto>(HttpMethod.Post, "api/billing/invoices", req);
    public Task<PaymentDto?> RecordPaymentAsync(RecordPaymentRequest req) => SendAsync<PaymentDto>(HttpMethod.Post, "api/billing/payments", req);
    public Task<StripeCheckoutResponse?> CreateStripeCheckoutAsync(StripeCheckoutRequest req) => SendAsync<StripeCheckoutResponse>(HttpMethod.Post, "api/billing/stripe/checkout", req);

    // Platform Admin (Multi-Product & Multi-Tenant)
    public Task<List<ProductDto>?> GetProductsAsync() => SendAsync<List<ProductDto>>(HttpMethod.Get, "api/platform/products");
    public Task<ProductDto?> CreateProductAsync(CreateProductRequest req) => SendAsync<ProductDto>(HttpMethod.Post, "api/platform/products", req);
    public Task<List<TenantDto>?> GetTenantsAsync() => SendAsync<List<TenantDto>>(HttpMethod.Get, "api/platform/tenants");
    public Task<TenantDto?> CreateTenantAsync(CreateTenantRequest req) => SendAsync<TenantDto>(HttpMethod.Post, "api/platform/tenants", req);
    public Task ToggleTenantStatusAsync(Guid tenantId) => SendVoidAsync(HttpMethod.Post, $"api/platform/tenants/{tenantId}/toggle-status");
    public Task<List<TenantProductSubscriptionDto>?> GetSubscriptionsAsync() => SendAsync<List<TenantProductSubscriptionDto>>(HttpMethod.Get, "api/platform/subscriptions");
    public Task<TenantProductSubscriptionDto?> SubscribeTenantProductAsync(SubscribeTenantProductRequest req) => SendAsync<TenantProductSubscriptionDto>(HttpMethod.Post, "api/platform/subscriptions", req);

    // Tenant-Specific Controls (Branding, Contact, Door Lock Hardware)
    public Task<TenantSettingsDto?> GetTenantSettingsAsync() => SendAsync<TenantSettingsDto>(HttpMethod.Get, "api/tenant-settings");
    public Task<TenantSettingsDto?> UpdateTenantSettingsAsync(UpdateTenantSettingsRequest req) => SendAsync<TenantSettingsDto>(HttpMethod.Put, "api/tenant-settings", req);

    // Member Portal (Self-Service Check-In + Door Unlock, Membership, Classes, Workouts)
    public Task<MemberPortalOverviewDto?> GetMemberPortalOverviewAsync() => SendAsync<MemberPortalOverviewDto>(HttpMethod.Get, "api/member-portal/overview");
    public Task<DoorUnlockResultDto?> CheckInAndUnlockDoorAsync(DoorUnlockRequest req) => SendAsync<DoorUnlockResultDto>(HttpMethod.Post, "api/member-portal/checkin-unlock", req);
    public Task<MemberDto?> UpdateSelfMembershipAsync(UpdateSelfMembershipRequest req) => SendAsync<MemberDto>(HttpMethod.Post, "api/member-portal/membership", req);
    public Task<ClassBookingDto?> BookMyClassAsync(Guid sessionId) => SendAsync<ClassBookingDto>(HttpMethod.Post, $"api/member-portal/classes/{sessionId}/book");
    public Task CancelMyBookingAsync(Guid bookingId) => SendVoidAsync(HttpMethod.Post, $"api/member-portal/bookings/{bookingId}/cancel");
    public Task<WorkoutLogDto?> LogWorkoutAsync(CreateWorkoutLogRequest req) => SendAsync<WorkoutLogDto>(HttpMethod.Post, "api/member-portal/workouts", req);
    public Task DeleteWorkoutAsync(Guid workoutId) => SendVoidAsync(HttpMethod.Delete, $"api/member-portal/workouts/{workoutId}");
    public Task<MemberDto?> UpdateMyProfileAsync(UpdateMemberProfileRequest req) => SendAsync<MemberDto>(HttpMethod.Put, "api/member-portal/profile", req);
    public Task<MemberDto?> SignMyWaiverAsync(SignWaiverRequest req) => SendAsync<MemberDto>(HttpMethod.Post, "api/member-portal/waiver", req);

    // Public Branded Online Join & Waiver
    public Task<PublicJoinInfoDto?> GetPublicJoinInfoAsync(string tenantSlug) => SendAsync<PublicJoinInfoDto>(HttpMethod.Get, $"api/public/join/{Uri.EscapeDataString(tenantSlug)}");
    public Task<AuthResponse?> JoinOnlineAsync(string tenantSlug, PublicJoinRequest req) => SendAsync<AuthResponse>(HttpMethod.Post, $"api/public/join/{Uri.EscapeDataString(tenantSlug)}", req);

    // Staff Point of Sale (POS / Pro Shop)
    public Task<List<PosProductDto>?> GetPosProductsAsync() => SendAsync<List<PosProductDto>>(HttpMethod.Get, "api/pos/products");
    public Task<PosProductDto?> CreatePosProductAsync(UpsertPosProductRequest req) => SendAsync<PosProductDto>(HttpMethod.Post, "api/pos/products", req);
    public Task<List<PosSaleDto>?> GetPosSalesAsync() => SendAsync<List<PosSaleDto>>(HttpMethod.Get, "api/pos/sales");
    public Task<PosSaleDto?> RecordPosSaleAsync(CreatePosSaleRequest req) => SendAsync<PosSaleDto>(HttpMethod.Post, "api/pos/sales", req);

    // Personal Training & Resource Bookings
    public Task<List<BookableResourceDto>?> GetResourcesAsync() => SendAsync<List<BookableResourceDto>>(HttpMethod.Get, "api/resources");
    public Task<BookableResourceDto?> CreateResourceAsync(CreateBookableResourceRequest req) => SendAsync<BookableResourceDto>(HttpMethod.Post, "api/resources", req);
    public Task<List<ResourceBookingDto>?> GetResourceBookingsAsync() => SendAsync<List<ResourceBookingDto>>(HttpMethod.Get, "api/resources/bookings");
    public Task<ResourceBookingDto?> BookResourceAsync(CreateResourceBookingRequest req) => SendAsync<ResourceBookingDto>(HttpMethod.Post, "api/resources/bookings", req);
    public Task CancelResourceBookingAsync(Guid id) => SendVoidAsync(HttpMethod.Post, $"api/resources/bookings/{id}/cancel");

    // Prospect / Lead CRM Pipeline
    public Task<List<ProspectLeadDto>?> GetLeadsAsync(string? stage = null) =>
        SendAsync<List<ProspectLeadDto>>(HttpMethod.Get, string.IsNullOrWhiteSpace(stage) ? "api/crm/leads" : $"api/crm/leads?stage={Uri.EscapeDataString(stage)}");
    public Task<ProspectLeadDto?> CreateLeadAsync(CreateProspectLeadRequest req) => SendAsync<ProspectLeadDto>(HttpMethod.Post, "api/crm/leads", req);
    public Task<ProspectLeadDto?> UpdateLeadStageAsync(Guid id, UpdateProspectLeadStageRequest req) => SendAsync<ProspectLeadDto>(HttpMethod.Put, $"api/crm/leads/{id}/stage", req);
    public Task<MemberDto?> ConvertLeadAsync(Guid id) => SendAsync<MemberDto>(HttpMethod.Post, $"api/crm/leads/{id}/convert");

    // Club Announcements & Member Inbox
    public Task<List<ClubAnnouncementDto>?> GetAnnouncementsAsync() => SendAsync<List<ClubAnnouncementDto>>(HttpMethod.Get, "api/communications/announcements");
    public Task<ClubAnnouncementDto?> PublishAnnouncementAsync(CreateClubAnnouncementRequest req) => SendAsync<ClubAnnouncementDto>(HttpMethod.Post, "api/communications/announcements", req);
    public Task DeleteAnnouncementAsync(Guid id) => SendVoidAsync(HttpMethod.Delete, $"api/communications/announcements/{id}");

    // Staff Door Access Monitor & Remote Unlock
    public Task<List<DoorAccessEventDto>?> GetDoorAccessEventsAsync() => SendAsync<List<DoorAccessEventDto>>(HttpMethod.Get, "api/door-access/events");
    public Task<DoorUnlockResultDto?> RemoteUnlockDoorAsync(DoorUnlockRequest req) => SendAsync<DoorUnlockResultDto>(HttpMethod.Post, "api/door-access/remote-unlock", req);

    // Staff Reports & CSV Export
    public Task<StaffReportsDto?> GetReportsSummaryAsync() => SendAsync<StaffReportsDto>(HttpMethod.Get, "api/reports/summary");
    public async Task<string> DownloadCsvAsync(string relativeUrl)
    {
        using var req = CreateRequest(HttpMethod.Get, relativeUrl);
        using var res = await http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
    }
}

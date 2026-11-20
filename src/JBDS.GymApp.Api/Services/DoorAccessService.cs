using System.Net.Http.Json;

namespace JBDS.GymApp.Api.Services;

public sealed record DoorUnlockHardwareResult(
    bool Success,
    int UnlockDurationSeconds,
    bool IsSimulated,
    string ProviderMessage);

public interface IDoorAccessService
{
    Task<DoorUnlockHardwareResult> UnlockDoorAsync(
        Guid tenantId,
        Guid memberId,
        string memberName,
        string doorName,
        CancellationToken ct = default);
}

/// <summary>
/// Triggers physical gym door access controllers (e.g., Kisi, Brivo, Salto, UniFi Access, Shelly relay)
/// via a configured HTTP webhook (`DoorAccess:WebhookUrl`). Falls back to a safe simulated 5-second
/// relay unlock when no hardware webhook is configured yet.
/// </summary>
public sealed class WebhookDoorAccessService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookDoorAccessService> logger) : IDoorAccessService
{
    public async Task<DoorUnlockHardwareResult> UnlockDoorAsync(
        Guid tenantId,
        Guid memberId,
        string memberName,
        string doorName,
        CancellationToken ct = default)
    {
        var durationSeconds = configuration.GetValue("DoorAccess:UnlockDurationSeconds", 5);
        var webhookUrl = configuration["DoorAccess:WebhookUrl"];
        var apiKey = configuration["DoorAccess:ApiKey"];

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            logger.LogInformation(
                "Simulated door unlock: Tenant={TenantId}, Member={MemberName}, Door={DoorName}, Duration={Duration}s",
                tenantId, memberName, doorName, durationSeconds);

            return new DoorUnlockHardwareResult(
                Success: true,
                UnlockDurationSeconds: durationSeconds,
                IsSimulated: true,
                ProviderMessage: $"Simulated relay unlocked '{doorName}' for {durationSeconds}s (configure DoorAccess:WebhookUrl for physical lock hardware).");
        }

        try
        {
            var client = httpClientFactory.CreateClient("DoorAccess");
            using var req = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = JsonContent.Create(new
                {
                    tenantId,
                    memberId,
                    memberName,
                    doorName,
                    durationSeconds,
                    timestampUtc = DateTime.UtcNow
                })
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                req.Headers.Add("X-Door-Api-Key", apiKey);
            }

            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            return new DoorUnlockHardwareResult(
                Success: resp.IsSuccessStatusCode,
                UnlockDurationSeconds: durationSeconds,
                IsSimulated: false,
                ProviderMessage: resp.IsSuccessStatusCode
                    ? $"Hardware relay unlocked '{doorName}' for {durationSeconds}s."
                    : $"Hardware controller returned {(int)resp.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Door unlock webhook failed for Door={DoorName}", doorName);
            return new DoorUnlockHardwareResult(
                Success: false,
                UnlockDurationSeconds: 0,
                IsSimulated: false,
                ProviderMessage: $"Door controller unreachable: {ex.Message}");
        }
    }
}

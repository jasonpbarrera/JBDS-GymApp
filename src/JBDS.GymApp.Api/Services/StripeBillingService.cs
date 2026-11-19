using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Shared.DTOs;
using Stripe;
using Stripe.Checkout;

namespace JBDS.GymApp.Api.Services;

/// <summary>
/// Wraps Stripe Checkout and webhook verification. Automatically falls back to a safe simulated
/// checkout URL when Stripe:SecretKey is not configured (for zero-cost local development).
/// </summary>
public sealed class StripeBillingService(IConfiguration configuration)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]);

    public async Task<StripeCheckoutResponse> CreateCheckoutSessionAsync(
        Data.Entities.Invoice invoice,
        Member member,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default)
    {
        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            var simId = $"cs_test_sim_{Guid.NewGuid():N}";
            var sep = successUrl.Contains('?') ? "&" : "?";
            return new StripeCheckoutResponse(
                CheckoutUrl: $"{successUrl}{sep}simulated_checkout=true&invoice={invoice.Id}&session_id={simId}",
                SessionId: simId,
                IsSimulated: true);
        }

        StripeConfiguration.ApiKey = secretKey;

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = member.Email,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["invoice_id"] = invoice.Id.ToString(),
                ["tenant_id"] = invoice.TenantId.ToString(),
                ["product_id"] = invoice.ProductId.ToString()
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = invoice.Currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(invoice.Amount * 100m),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = invoice.Description
                        }
                    }
                }
            ]
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return new StripeCheckoutResponse(session.Url, session.Id, IsSimulated: false);
    }
}

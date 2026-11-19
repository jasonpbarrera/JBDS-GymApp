using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Api.Services;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace JBDS.GymApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BillingController(
    GymAppDbContext db,
    StripeBillingService stripeBilling,
    ITenantResolverService tenantResolver,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("invoices")]
    public async Task<ActionResult<List<InvoiceDto>>> GetInvoices(CancellationToken ct)
    {
        var invoices = await db.Invoices
            .AsNoTracking()
            .Include(i => i.Member)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvoiceDto(
                i.Id,
                i.MemberId,
                i.Member != null ? $"{i.Member.FirstName} {i.Member.LastName}".Trim() : "Unknown",
                i.InvoiceNumber,
                i.Amount,
                i.Currency,
                i.Status,
                i.DueDateUtc,
                i.PaidAtUtc,
                i.Description,
                i.StripeInvoiceId))
            .ToListAsync(ct);

        return Ok(invoices);
    }

    [HttpPost("invoices")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct);
        if (member is null) return NotFound(new { error = "Member not found." });

        var invoice = new Data.Entities.Invoice
        {
            MemberId = member.Id,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.ToUpperInvariant(),
            Description = request.Description.Trim(),
            DueDateUtc = request.DueDateUtc ?? DateTime.UtcNow.AddDays(7),
            Status = InvoiceStatus.Open
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetInvoices), new InvoiceDto(
            invoice.Id, invoice.MemberId, $"{member.FirstName} {member.LastName}".Trim(),
            invoice.InvoiceNumber, invoice.Amount, invoice.Currency, invoice.Status,
            invoice.DueDateUtc, invoice.PaidAtUtc, invoice.Description, invoice.StripeInvoiceId));
    }

    [HttpGet("payments")]
    public async Task<ActionResult<List<PaymentDto>>> GetPayments(CancellationToken ct)
    {
        var payments = await db.Payments
            .AsNoTracking()
            .Include(p => p.Member)
            .OrderByDescending(p => p.PaidAtUtc)
            .Select(p => new PaymentDto(
                p.Id,
                p.InvoiceId,
                p.MemberId,
                p.Member != null ? $"{p.Member.FirstName} {p.Member.LastName}".Trim() : "Unknown",
                p.Amount,
                p.Currency,
                p.Method,
                p.Status,
                p.PaidAtUtc,
                p.Notes))
            .ToListAsync(ct);

        return Ok(payments);
    }

    [HttpPost("payments")]
    [Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
    public async Task<ActionResult<PaymentDto>> RecordPayment([FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var invoice = await db.Invoices
            .Include(i => i.Member)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);

        if (invoice is null) return NotFound(new { error = "Invoice not found." });

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            MemberId = invoice.MemberId,
            Amount = request.Amount,
            Currency = invoice.Currency,
            Method = request.Method,
            Status = "Succeeded",
            PaidAtUtc = DateTime.UtcNow,
            Notes = request.Notes?.Trim()
        };

        db.Payments.Add(payment);

        var totalPaid = invoice.Payments.Where(p => p.Status == "Succeeded").Sum(p => p.Amount) + request.Amount;
        if (totalPaid >= invoice.Amount)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new PaymentDto(
            payment.Id, payment.InvoiceId, payment.MemberId,
            invoice.Member != null ? $"{invoice.Member.FirstName} {invoice.Member.LastName}".Trim() : "Unknown",
            payment.Amount, payment.Currency, payment.Method, payment.Status, payment.PaidAtUtc, payment.Notes));
    }

    [HttpPost("stripe/checkout")]
    public async Task<ActionResult<StripeCheckoutResponse>> CreateStripeCheckout(
        [FromBody] StripeCheckoutRequest request,
        CancellationToken ct)
    {
        var invoice = await db.Invoices
            .Include(i => i.Member)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);

        if (invoice?.Member is null) return NotFound(new { error = "Invoice not found." });
        if (invoice.Status == InvoiceStatus.Paid) return BadRequest(new { error = "Invoice is already paid." });

        var checkout = await stripeBilling.CreateCheckoutSessionAsync(
            invoice, invoice.Member, request.SuccessUrl, request.CancelUrl, ct);

        invoice.StripeCheckoutSessionId = checkout.SessionId;
        await db.SaveChangesAsync(ct);

        return Ok(checkout);
    }

    [HttpPost("stripe/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(ct);
        var webhookSecret = configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return Ok(new { received = true, mode = "unconfigured" });
        }

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);

            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted &&
                stripeEvent.Data.Object is Session session &&
                session.Metadata.TryGetValue("invoice_id", out var invIdRaw) &&
                Guid.TryParse(invIdRaw, out var invoiceId) &&
                session.Metadata.TryGetValue("tenant_id", out var tenantIdRaw) &&
                Guid.TryParse(tenantIdRaw, out var tenantId) &&
                session.Metadata.TryGetValue("product_id", out var productIdRaw) &&
                Guid.TryParse(productIdRaw, out var productId))
            {
                using (tenantResolver.BeginScope(tenantId, productId, "StripeWebhook"))
                {
                    var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
                    if (invoice is not null && invoice.Status != InvoiceStatus.Paid)
                    {
                        invoice.Status = InvoiceStatus.Paid;
                        invoice.PaidAtUtc = DateTime.UtcNow;

                        db.Payments.Add(new Payment
                        {
                            InvoiceId = invoice.Id,
                            MemberId = invoice.MemberId,
                            Amount = invoice.Amount,
                            Currency = invoice.Currency,
                            Method = Shared.Enums.PaymentMethod.StripeCard,
                            StripePaymentIntentId = session.PaymentIntentId,
                            Status = "Succeeded",
                            PaidAtUtc = DateTime.UtcNow,
                            Notes = "Paid via Stripe Checkout"
                        });

                        await db.SaveChangesAsync(ct);
                    }
                }
            }

            return Ok(new { received = true });
        }
        catch (StripeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

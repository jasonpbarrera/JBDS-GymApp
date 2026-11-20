using System.Security.Claims;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Shared.DTOs;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Controllers;

/// <summary>
/// Staff Point of Sale (POS / Pro Shop) for drinks, supplements, merch, and day passes.
/// </summary>
[ApiController]
[Route("api/pos")]
[Authorize(Roles = $"{PlatformRoles.PlatformAdmin},{PlatformRoles.TenantAdmin},{PlatformRoles.Staff}")]
public sealed class PosController(GymAppDbContext db) : ControllerBase
{
    [HttpGet("products")]
    public async Task<ActionResult<List<PosProductDto>>> GetProducts(CancellationToken ct)
    {
        var list = await db.PosProducts
            .AsNoTracking()
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .Select(p => new PosProductDto(p.Id, p.Name, p.Sku, p.Category, p.Price, p.StockQuantity, p.IsActive))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("products")]
    public async Task<ActionResult<PosProductDto>> CreateProduct([FromBody] UpsertPosProductRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Product name is required." });

        var entity = new PosProduct
        {
            Name = request.Name.Trim(),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? $"SKU-{Random.Shared.Next(1000, 9999)}" : request.Sku.Trim().ToUpperInvariant(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Retail" : request.Category.Trim(),
            Price = request.Price,
            StockQuantity = Math.Max(0, request.StockQuantity),
            IsActive = request.IsActive
        };

        db.PosProducts.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetProducts), new PosProductDto(
            entity.Id, entity.Name, entity.Sku, entity.Category, entity.Price, entity.StockQuantity, entity.IsActive));
    }

    [HttpGet("sales")]
    public async Task<ActionResult<List<PosSaleDto>>> GetSales(CancellationToken ct)
    {
        var sales = await db.PosSales
            .AsNoTracking()
            .OrderByDescending(s => s.SoldAtUtc)
            .Take(50)
            .Select(s => new PosSaleDto(s.Id, s.MemberId, s.BuyerName, s.TotalAmount, s.PaymentMethod, s.SoldAtUtc, s.ItemsSummary))
            .ToListAsync(ct);
        return Ok(sales);
    }

    [HttpPost("sales")]
    public async Task<ActionResult<PosSaleDto>> RecordSale([FromBody] CreatePosSaleRequest request, CancellationToken ct)
    {
        var product = await db.PosProducts.FirstOrDefaultAsync(p => p.Id == request.PosProductId, ct);
        if (product is null) return NotFound(new { error = "POS item not found." });

        var qty = Math.Clamp(request.Quantity, 1, 100);
        if (product.StockQuantity < qty)
            return BadRequest(new { error = $"Insufficient stock for '{product.Name}' ({product.StockQuantity} left)." });

        Member? member = null;
        if (request.MemberId.HasValue)
        {
            member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId.Value, ct);
        }

        var total = product.Price * qty;
        var buyerName = member is not null
            ? $"{member.FirstName} {member.LastName}".Trim()
            : (string.IsNullOrWhiteSpace(request.WalkInBuyerName) ? "Walk-In Guest" : request.WalkInBuyerName.Trim());

        product.StockQuantity -= qty;
        Guid? staffId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;

        var sale = new PosSale
        {
            MemberId = member?.Id,
            BuyerName = buyerName,
            TotalAmount = total,
            PaymentMethod = request.PaymentMethod,
            SoldByUserId = staffId,
            ItemsSummary = $"{qty}x {product.Name} (@ ${product.Price:N2})"
        };

        db.PosSales.Add(sale);

        if (request.ChargeToMemberAccount && member is not null)
        {
            db.Invoices.Add(new Invoice
            {
                MemberId = member.Id,
                Amount = total,
                Currency = "USD",
                Status = InvoiceStatus.Open,
                DueDateUtc = DateTime.UtcNow.AddDays(7),
                Description = $"POS Charge: {sale.ItemsSummary}"
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok(new PosSaleDto(sale.Id, sale.MemberId, sale.BuyerName, sale.TotalAmount, sale.PaymentMethod, sale.SoldAtUtc, sale.ItemsSummary));
    }
}

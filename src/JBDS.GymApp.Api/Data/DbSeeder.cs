using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Data;

/// <summary>
/// Seeds default multi-product, multi-tenant data for local development and first-time bootstrap.
/// Demonstrates shared Tenants subscribed to multiple Products with strict TenantId + ProductId isolation.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GymAppDbContext>();
        var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolverService>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        using (tenantResolver.BeginUnrestrictedScope())
        {
            foreach (var role in PlatformRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new ApplicationRole(role));
                }
            }

            if (await db.Products.AnyAsync())
            {
                return;
            }

            // 1. Seed Products (multi-product platform)
            var gymProduct = new Product
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Key = PlatformProductKeys.GymMembership,
                Name = "JBDS Gym Membership",
                Description = "Full-featured gym membership, QR check-in, class booking, and billing PWA.",
                BaseDomain = "gymapp.jbdigitalservices.net",
                IsActive = true
            };

            var ptProduct = new Product
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Key = "personal-training",
                Name = "JBDS Personal Training Suite",
                Description = "1-on-1 coaching, workout programming, and nutrition tracking product.",
                BaseDomain = "pt.jbdigitalservices.net",
                IsActive = true
            };

            db.Products.AddRange(gymProduct, ptProduct);

            // 2. Seed Shared Tenants
            var ironForge = new Tenant
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Slug = "ironforge",
                Name = "Iron Forge Fitness",
                ContactEmail = "admin@ironforge.demo",
                Phone = "(615) 555-0192",
                TimeZoneId = "America/Chicago",
                PrimaryColor = "#10b981",
                AccentColor = "#0ea5e9",
                IsActive = true
            };

            var apexGym = new Tenant
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Slug = "apex",
                Name = "Apex Athletics Club",
                ContactEmail = "admin@apex.demo",
                Phone = "(615) 555-0148",
                TimeZoneId = "America/Chicago",
                PrimaryColor = "#f59e0b",
                AccentColor = "#ef4444",
                IsActive = true
            };

            db.Tenants.AddRange(ironForge, apexGym);

            // 3. Many-to-many Tenant <-> Product Subscriptions
            // Iron Forge subscribes to BOTH Gym Membership and Personal Training!
            db.TenantProductSubscriptions.AddRange(
                new TenantProductSubscription
                {
                    TenantId = ironForge.Id,
                    ProductId = gymProduct.Id,
                    CustomSubdomain = "ironforge",
                    Tier = "Pro",
                    Status = "Active"
                },
                new TenantProductSubscription
                {
                    TenantId = ironForge.Id,
                    ProductId = ptProduct.Id,
                    CustomSubdomain = "ironforge-pt",
                    Tier = "Standard",
                    Status = "Active"
                },
                new TenantProductSubscription
                {
                    TenantId = apexGym.Id,
                    ProductId = gymProduct.Id,
                    CustomSubdomain = "apex",
                    Tier = "Standard",
                    Status = "Active"
                });

            await db.SaveChangesAsync();

            // 4. Seed Demo Users
            await EnsureUserAsync(userManager, "platform@jbdigitalservices.net", "Platform123!", "Jason Barrera (Platform Admin)", ironForge.Id, true, [PlatformRoles.PlatformAdmin, PlatformRoles.TenantAdmin]);
            await EnsureUserAsync(userManager, "admin@ironforge.demo", "IronForge123!", "Marcus Vance (Gym Admin)", ironForge.Id, false, [PlatformRoles.TenantAdmin]);
            await EnsureUserAsync(userManager, "staff@ironforge.demo", "Staff123!", "Elena Rostova (Front Desk)", ironForge.Id, false, [PlatformRoles.Staff]);
            await EnsureUserAsync(userManager, "admin@apex.demo", "Apex123!", "Devon Brooks (Apex Admin)", apexGym.Id, false, [PlatformRoles.TenantAdmin]);

            // 5. Seed Tenant-Scoped Gym Data using scoped TenantResolverService
            await SeedGymTenantDataAsync(db, tenantResolver, ironForge.Id, gymProduct.Id, "Iron Forge", 49m, 89m);
            await SeedGymTenantDataAsync(db, tenantResolver, apexGym.Id, gymProduct.Id, "Apex Athletics", 59m, 109m);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName,
        Guid tenantId,
        bool isPlatformAdmin,
        string[] roles)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            TenantId = tenantId,
            IsPlatformAdmin = isPlatformAdmin
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRolesAsync(user, roles);
        }
    }

    private static async Task SeedGymTenantDataAsync(
        GymAppDbContext db,
        ITenantResolverService tenantResolver,
        Guid tenantId,
        Guid productId,
        string prefix,
        decimal basicPrice,
        decimal proPrice)
    {
        using (tenantResolver.BeginScope(tenantId, productId, "Seeder"))
        {
            var basicPlan = new MembershipPlan
            {
                Name = $"{prefix} Essentials",
                Description = "24/7 gym floor access, locker rooms, and mobile QR check-in.",
                Price = basicPrice,
                BillingInterval = BillingInterval.Monthly,
                IncludesClasses = false,
                IsActive = true
            };

            var proPlan = new MembershipPlan
            {
                Name = $"{prefix} Unlimited + Classes",
                Description = "Unlimited 24/7 gym access, all group fitness classes, and guest passes.",
                Price = proPrice,
                BillingInterval = BillingInterval.Monthly,
                IncludesClasses = true,
                MaxClassBookingsPerWeek = 14,
                IsActive = true
            };

            db.MembershipPlans.AddRange(basicPlan, proPlan);
            await db.SaveChangesAsync();

            var member1 = new Member
            {
                FirstName = "Jordan",
                LastName = "Taylor",
                Email = $"jordan.{prefix.ToLowerInvariant().Replace(" ", "")}@example.com",
                Phone = "(615) 555-0101",
                Status = MembershipStatus.Active,
                QrCodeToken = $"GYM-{prefix[..2].ToUpperInvariant()}-1001",
                BarcodeNumber = $"{(prefix.StartsWith("Iron") ? "901" : "902")}100001001"
            };

            var member2 = new Member
            {
                FirstName = "Samantha",
                LastName = "Reed",
                Email = $"sam.{prefix.ToLowerInvariant().Replace(" ", "")}@example.com",
                Phone = "(615) 555-0102",
                Status = MembershipStatus.Active,
                QrCodeToken = $"GYM-{prefix[..2].ToUpperInvariant()}-1002",
                BarcodeNumber = $"{(prefix.StartsWith("Iron") ? "901" : "902")}100001002"
            };

            db.Members.AddRange(member1, member2);
            await db.SaveChangesAsync();

            var sub1 = new MemberSubscription
            {
                MemberId = member1.Id,
                MembershipPlanId = proPlan.Id,
                Status = MembershipStatus.Active,
                StartDateUtc = DateTime.UtcNow.AddDays(-20),
                NextBillingDateUtc = DateTime.UtcNow.AddDays(10),
                AutoRenew = true
            };

            var sub2 = new MemberSubscription
            {
                MemberId = member2.Id,
                MembershipPlanId = basicPlan.Id,
                Status = MembershipStatus.Active,
                StartDateUtc = DateTime.UtcNow.AddDays(-12),
                NextBillingDateUtc = DateTime.UtcNow.AddDays(18),
                AutoRenew = true
            };

            db.MemberSubscriptions.AddRange(sub1, sub2);

            db.CheckIns.AddRange(
                new CheckIn
                {
                    MemberId = member1.Id,
                    CheckedInAtUtc = DateTime.UtcNow.AddHours(-2),
                    Method = CheckInMethod.QrCode,
                    IsAllowed = true,
                    Notes = "Morning workout"
                },
                new CheckIn
                {
                    MemberId = member2.Id,
                    CheckedInAtUtc = DateTime.UtcNow.AddMinutes(-40),
                    Method = CheckInMethod.Barcode,
                    IsAllowed = true
                });

            var hiitClass = new GymClass
            {
                Name = "HIIT Strength & Conditioning",
                Description = "High-intensity kettlebell and barbell circuit.",
                Category = "HIIT",
                TrainerName = "Coach Alex Mercer",
                DefaultCapacity = 16,
                DurationMinutes = 50,
                LocationRoom = "Turf Zone A"
            };

            var mobilityClass = new GymClass
            {
                Name = "Power Yoga & Mobility",
                Description = "Active recovery, core stability, and joint mobility.",
                Category = "Recovery",
                TrainerName = "Coach Maya Lin",
                DefaultCapacity = 20,
                DurationMinutes = 60,
                LocationRoom = "Studio B"
            };

            db.GymClasses.AddRange(hiitClass, mobilityClass);
            await db.SaveChangesAsync();

            var session1 = new ClassSession
            {
                GymClassId = hiitClass.Id,
                StartTimeUtc = DateTime.UtcNow.AddHours(3),
                EndTimeUtc = DateTime.UtcNow.AddHours(3).AddMinutes(50),
                Capacity = 16,
                TrainerName = hiitClass.TrainerName,
                LocationRoom = hiitClass.LocationRoom
            };

            var session2 = new ClassSession
            {
                GymClassId = mobilityClass.Id,
                StartTimeUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
                EndTimeUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(15),
                Capacity = 20,
                TrainerName = mobilityClass.TrainerName,
                LocationRoom = mobilityClass.LocationRoom
            };

            db.ClassSessions.AddRange(session1, session2);
            await db.SaveChangesAsync();

            db.ClassBookings.Add(new ClassBooking
            {
                ClassSessionId = session1.Id,
                MemberId = member1.Id,
                Status = BookingStatus.Confirmed
            });

            var paidInvoice = new Invoice
            {
                MemberId = member1.Id,
                MemberSubscriptionId = sub1.Id,
                InvoiceNumber = $"INV-{prefix[..2].ToUpperInvariant()}-2026-001",
                Amount = proPrice,
                Status = InvoiceStatus.Paid,
                DueDateUtc = DateTime.UtcNow.AddDays(-15),
                PaidAtUtc = DateTime.UtcNow.AddDays(-15),
                Description = $"{proPlan.Name} - Monthly Renewal"
            };

            var openInvoice = new Invoice
            {
                MemberId = member2.Id,
                MemberSubscriptionId = sub2.Id,
                InvoiceNumber = $"INV-{prefix[..2].ToUpperInvariant()}-2026-002",
                Amount = basicPrice,
                Status = InvoiceStatus.Open,
                DueDateUtc = DateTime.UtcNow.AddDays(5),
                Description = $"{basicPlan.Name} - Upcoming Renewal"
            };

            db.Invoices.AddRange(paidInvoice, openInvoice);
            await db.SaveChangesAsync();

            db.Payments.Add(new Payment
            {
                InvoiceId = paidInvoice.Id,
                MemberId = member1.Id,
                Amount = proPrice,
                Method = PaymentMethod.StripeCard,
                Status = "Succeeded",
                PaidAtUtc = DateTime.UtcNow.AddDays(-15),
                Notes = "Auto-pay monthly subscription"
            });

            await db.SaveChangesAsync();
        }
    }
}

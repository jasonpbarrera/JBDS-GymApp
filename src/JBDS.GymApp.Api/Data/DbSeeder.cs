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

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        using (tenantResolver.BeginUnrestrictedScope())
        {
            foreach (var role in PlatformRoles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new ApplicationRole(role));
                }
            }

            var ironForgeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var apexId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var superiorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
            var gymProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            if (await db.Products.AnyAsync())
            {
                await EnsureDemoMembersAsync(db, userManager, tenantResolver, ironForgeId, apexId, gymProductId);
                await EnsureSuperiorFitnessAndGymMasterModulesAsync(db, userManager, tenantResolver, ironForgeId, apexId, superiorId, gymProductId);
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

            // 6. Seed Demo Member Portal accounts & sample workout logs
            await EnsureDemoMembersAsync(db, userManager, tenantResolver, ironForge.Id, apexGym.Id, gymProduct.Id);
            await EnsureSuperiorFitnessAndGymMasterModulesAsync(db, userManager, tenantResolver, ironForge.Id, apexGym.Id, superiorId, gymProduct.Id);
        }
    }

    private static async Task EnsureSuperiorFitnessAndGymMasterModulesAsync(
        GymAppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolverService tenantResolver,
        Guid ironForgeId,
        Guid apexId,
        Guid superiorId,
        Guid gymProductId)
    {
        // Set distinct theme presets on existing demo tenants
        var ironTenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == ironForgeId);
        if (ironTenant is not null && string.IsNullOrWhiteSpace(ironTenant.HeroTagline))
        {
            ironTenant.ThemePreset = "iron-dark";
            ironTenant.HeroTagline = "Forge Your Strength — 24/7 Industrial Training Facility";
        }
        var apexTenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == apexId);
        if (apexTenant is not null && string.IsNullOrWhiteSpace(apexTenant.HeroTagline))
        {
            apexTenant.ThemePreset = "apex-bold";
            apexTenant.HeroTagline = "Peak Athletic Performance & Competition Coaching";
        }

        // Seed real modeled tenant: Superior Fitness Cleveland (superiorfitness-cleveland.com)
        var superior = await db.Tenants.FirstOrDefaultAsync(t => t.Id == superiorId || t.Slug == "superiorfitness");
        if (superior is null)
        {
            superior = new Tenant
            {
                Id = superiorId,
                Slug = "superiorfitness",
                Name = "Superior Fitness Cleveland",
                CustomDomain = "superiorfitness-cleveland.com",
                ContactEmail = "info@superiorfitness-cleveland.com",
                Phone = "(423) 310-2899",
                TimeZoneId = "America/New_York",
                PrimaryColor = "#dc2626",
                AccentColor = "#f59e0b",
                ThemePreset = "superior-crimson",
                HeroTagline = "Stronger Together At Our Gym — 24/7 Access · 1-on-1 Personal Training · Cleveland, TN",
                IsActive = true
            };
            db.Tenants.Add(superior);
            db.TenantProductSubscriptions.Add(new TenantProductSubscription
            {
                TenantId = superior.Id,
                ProductId = gymProductId,
                CustomSubdomain = "superiorfitness",
                CustomDomain = "superiorfitness-cleveland.com",
                Tier = "Pro",
                Status = "Active"
            });
            await db.SaveChangesAsync();

            await EnsureUserAsync(userManager, "admin@superiorfitness.demo", "Superior123!", "Superior Fitness Owner", superior.Id, false, [PlatformRoles.TenantAdmin]);
            await EnsureUserAsync(userManager, "staff@superiorfitness.demo", "Staff123!", "Superior Front Desk", superior.Id, false, [PlatformRoles.Staff]);
            await SeedGymTenantDataAsync(db, tenantResolver, superior.Id, gymProductId, "Superior Fitness", 39m, 69m);
            await EnsureMemberAccountAsync(db, userManager, tenantResolver, superior.Id, gymProductId,
                "member@superiorfitness.demo", "Member123!", "Logan", "Strickland", "GYM-SF-1001");
        }
        else
        {
            await db.SaveChangesAsync();
        }

        // Seed GymMaster modules (POS, PT/Facility Resources, CRM Leads, Club Announcements) per tenant
        foreach (var (tid, gymName) in new[] { (ironForgeId, "Iron Forge Fitness"), (apexId, "Apex Athletics Club"), (superior.Id, "Superior Fitness Cleveland") })
        {
            using (tenantResolver.BeginScope(tid, gymProductId, "Seeder"))
            {
                if (!await db.PosProducts.AnyAsync())
                {
                    db.PosProducts.AddRange(
                        new PosProduct { Name = "Ready-to-Drink Protein Shake", Sku = "DRK-PRO-01", Category = "Drinks", Price = 4.50m, StockQuantity = 48 },
                        new PosProduct { Name = "Pre-Workout Energy Can", Sku = "DRK-ENG-02", Category = "Drinks", Price = 3.75m, StockQuantity = 60 },
                        new PosProduct { Name = $"{gymName} Performance Tee", Sku = "MRC-TEE-01", Category = "Merch", Price = 28.00m, StockQuantity = 20 },
                        new PosProduct { Name = "Guest Day Pass", Sku = "PAS-DAY-01", Category = "Passes", Price = 15.00m, StockQuantity = 999 });
                }

                if (!await db.BookableResources.AnyAsync())
                {
                    db.BookableResources.AddRange(
                        new BookableResource { Name = "1-on-1 Personal Training Consultation", ResourceType = "PersonalTrainer", Description = "Strength assessment, goal setting, and custom nutrition guidance.", DurationMinutes = 60, Price = 65.00m },
                        new BookableResource { Name = "Strength Coaching Session (1-on-1)", ResourceType = "PersonalTrainer", Description = "Dedicated 1-on-1 barbell and hypertrophy coaching.", DurationMinutes = 50, Price = 55.00m },
                        new BookableResource { Name = "Infrared Recovery Sauna", ResourceType = "Sauna", Description = "Private 30-minute infrared recovery session.", DurationMinutes = 30, Price = 15.00m });
                }

                if (!await db.ProspectLeads.AnyAsync())
                {
                    db.ProspectLeads.AddRange(
                        new ProspectLead { FullName = "Lauren Blair", Email = "lauren.blair@example.com", Phone = "(423) 555-0181", Source = "Free Day Pass", Stage = "Trial", Notes = "Visited with dad, loved the gym floor." },
                        new ProspectLead { FullName = "Aaron Martinez", Email = "aaron.martinez@example.com", Phone = "(423) 555-0194", Source = "Website", Stage = "New", Notes = "Interested in 1-on-1 strength training." });
                }

                if (!await db.ClubAnnouncements.AnyAsync())
                {
                    db.ClubAnnouncements.AddRange(
                        new ClubAnnouncement
                        {
                            Title = $"Welcome to the {gymName} Member & Staff Portal!",
                            Body = "Use your mobile app for 24/7 door unlock, 6-digit keypad PIN access, class & 1-on-1 personal training bookings, and workout tracking.",
                            Category = "General",
                            IsPinned = true,
                            CreatedByName = $"{gymName} Management"
                        },
                        new ClubAnnouncement
                        {
                            Title = "1-on-1 Personal Training Consultations Open",
                            Body = "Book a strength training & nutrition consultation directly under the PT & Facilities tab.",
                            Category = "Promotion",
                            IsPinned = false,
                            CreatedByName = "Coaching Team"
                        });
                }

                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task EnsureDemoMembersAsync(
        GymAppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolverService tenantResolver,
        Guid ironForgeId,
        Guid apexId,
        Guid gymProductId)
    {
        await EnsureMemberAccountAsync(db, userManager, tenantResolver, ironForgeId, gymProductId,
            "member@ironforge.demo", "Member123!", "Jordan", "Taylor", "GYM-IR-1001");
        await EnsureMemberAccountAsync(db, userManager, tenantResolver, apexId, gymProductId,
            "member@apex.demo", "Member123!", "Jordan", "Taylor", "GYM-AP-1001");
    }

    private static async Task EnsureMemberAccountAsync(
        GymAppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITenantResolverService tenantResolver,
        Guid tenantId,
        Guid productId,
        string email,
        string password,
        string firstName,
        string lastName,
        string preferredQr)
    {
        await EnsureUserAsync(userManager, email, password, $"{firstName} {lastName}", tenantId, false, [PlatformRoles.Member]);
        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return;

        using (tenantResolver.BeginScope(tenantId, productId, "Seeder"))
        {
            var member = await db.Members.FirstOrDefaultAsync(m => m.UserId == user.Id || m.Email == email || m.QrCodeToken == preferredQr);
            if (member is null)
            {
                member = new Member
                {
                    UserId = user.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    Status = MembershipStatus.Active,
                    QrCodeToken = preferredQr
                };
                db.Members.Add(member);
                await db.SaveChangesAsync();
            }
            else if (member.UserId != user.Id)
            {
                member.UserId = user.Id;
                await db.SaveChangesAsync();
            }

            if (!await db.WorkoutLogs.AnyAsync(w => w.MemberId == member.Id))
            {
                db.WorkoutLogs.AddRange(
                    new WorkoutLog
                    {
                        MemberId = member.Id,
                        WorkoutDateUtc = DateTime.UtcNow.AddDays(-2),
                        Title = "Upper Body Strength",
                        Category = "Strength",
                        DurationMinutes = 55,
                        EstimatedCalories = 420,
                        ExercisesSummary = "Bench Press 4x8, Pull-Ups 4x10, Overhead Press 3x10, Cable Rows 3x12",
                        Notes = "Hit new PR on incline dumbbell press."
                    },
                    new WorkoutLog
                    {
                        MemberId = member.Id,
                        WorkoutDateUtc = DateTime.UtcNow.AddDays(-1),
                        Title = "HIIT Conditioning & Core",
                        Category = "HIIT",
                        DurationMinutes = 35,
                        EstimatedCalories = 380,
                        ExercisesSummary = "Rowing intervals 8x250m, Kettlebell Swings 4x15, Hanging Leg Raises 3x12"
                    });
                await db.SaveChangesAsync();
            }
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

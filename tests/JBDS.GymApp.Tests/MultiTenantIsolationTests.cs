using System.Security.Claims;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.GymApp.Api.Middleware;
using JBDS.GymApp.Shared.Enums;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.MultiTenancy;
using JBDS.Platform.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Tests;

public sealed class MultiTenantIsolationTests
{
    private static (GymAppDbContext Db, TenantResolverService Resolver) CreateInMemoryContext()
    {
        var resolver = new TenantResolverService();
        var options = new DbContextOptionsBuilder<GymAppDbContext>()
            .UseInMemoryDatabase($"GymAppTest_{Guid.NewGuid():N}")
            .Options;
        return (new GymAppDbContext(options, resolver), resolver);
    }

    [Fact]
    public async Task GlobalQueryFilters_IsolateTenantsAndAutoStampTenantAndProductIds()
    {
        var (db, resolver) = CreateInMemoryContext();
        var productId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (resolver.BeginScope(tenantA, productId))
        {
            db.Members.Add(new Member { FirstName = "Alice", LastName = "Iron", Email = "alice@iron.test" });
            await db.SaveChangesAsync();
        }

        using (resolver.BeginScope(tenantB, productId))
        {
            db.Members.Add(new Member { FirstName = "Bob", LastName = "Apex", Email = "bob@apex.test" });
            await db.SaveChangesAsync();
        }

        // Verify Tenant A sees ONLY Alice and her TenantId/ProductId were auto-stamped
        using (resolver.BeginScope(tenantA, productId))
        {
            var membersA = await db.Members.ToListAsync();
            Assert.Single(membersA);
            Assert.Equal("Alice", membersA[0].FirstName);
            Assert.Equal(tenantA, membersA[0].TenantId);
            Assert.Equal(productId, membersA[0].ProductId);
        }

        // Verify Tenant B sees ONLY Bob
        using (resolver.BeginScope(tenantB, productId))
        {
            var membersB = await db.Members.ToListAsync();
            Assert.Single(membersB);
            Assert.Equal("Bob", membersB[0].FirstName);
            Assert.Equal(tenantB, membersB[0].TenantId);
        }

        // Verify unrestricted scope sees both
        using (resolver.BeginUnrestrictedScope())
        {
            var all = await db.Members.ToListAsync();
            Assert.Equal(2, all.Count);
        }
    }

    [Fact]
    public async Task GlobalQueryFilters_IsolateMultipleProductsForSameSharedTenant()
    {
        var (db, resolver) = CreateInMemoryContext();
        var sharedTenantId = Guid.NewGuid();
        var gymProductId = Guid.NewGuid();
        var ptProductId = Guid.NewGuid();

        using (resolver.BeginScope(sharedTenantId, gymProductId))
        {
            db.MembershipPlans.Add(new MembershipPlan { Name = "Gym Unlimited", Price = 79m });
            await db.SaveChangesAsync();
        }

        using (resolver.BeginScope(sharedTenantId, ptProductId))
        {
            db.MembershipPlans.Add(new MembershipPlan { Name = "1-on-1 Coaching", Price = 299m });
            await db.SaveChangesAsync();
        }

        using (resolver.BeginScope(sharedTenantId, gymProductId))
        {
            var gymPlans = await db.MembershipPlans.ToListAsync();
            Assert.Single(gymPlans);
            Assert.Equal("Gym Unlimited", gymPlans[0].Name);
        }

        using (resolver.BeginScope(sharedTenantId, ptProductId))
        {
            var ptPlans = await db.MembershipPlans.ToListAsync();
            Assert.Single(ptPlans);
            Assert.Equal("1-on-1 Coaching", ptPlans[0].Name);
        }
    }

    [Fact]
    public async Task SaveChanges_BlocksCrossTenantInsertAttempt()
    {
        var (db, resolver) = CreateInMemoryContext();
        var productId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (resolver.BeginScope(tenantA, productId))
        {
            db.Members.Add(new Member
            {
                TenantId = tenantB, // Spoofed cross-tenant ID!
                FirstName = "Mallory",
                LastName = "Attacker",
                Email = "mallory@evil.test"
            });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task TenantResolutionMiddleware_PrioritizesSubdomainOverJwtClaim()
    {
        var (db, resolver) = CreateInMemoryContext();
        var product = new Product { Key = PlatformProductKeys.GymMembership, Name = "Gym", IsActive = true };
        var tenantSubdomain = new Tenant { Slug = "ironforge", Name = "Iron Forge", IsActive = true };
        var tenantJwt = new Tenant { Slug = "apex", Name = "Apex", IsActive = true };

        using (resolver.BeginUnrestrictedScope())
        {
            db.Products.Add(product);
            db.Tenants.AddRange(tenantSubdomain, tenantJwt);
            db.TenantProductSubscriptions.AddRange(
                new TenantProductSubscription { TenantId = tenantSubdomain.Id, ProductId = product.Id, CustomSubdomain = "ironforge", Status = "Active" },
                new TenantProductSubscription { TenantId = tenantJwt.Id, ProductId = product.Id, CustomSubdomain = "apex", Status = "Active" });
            await db.SaveChangesAsync();
        }

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Forwarded-Host"] = "ironforge.gymapp.jbdigitalservices.net";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PlatformClaimTypes.TenantId, tenantJwt.Id.ToString()),
            new Claim(ClaimTypes.Role, PlatformRoles.TenantAdmin)
        ], "TestAuth"));

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, db, resolver);

        Assert.Equal(tenantSubdomain.Id, resolver.CurrentTenantId);
        Assert.Equal("Subdomain", resolver.Current.ResolutionSource);
    }

    [Fact]
    public async Task TenantResolutionMiddleware_FallsBackToJwtClaimOnLocalhost()
    {
        var (db, resolver) = CreateInMemoryContext();
        var product = new Product { Key = PlatformProductKeys.GymMembership, Name = "Gym", IsActive = true };
        var tenantJwt = new Tenant { Slug = "apex", Name = "Apex", IsActive = true };

        using (resolver.BeginUnrestrictedScope())
        {
            db.Products.Add(product);
            db.Tenants.Add(tenantJwt);
            db.TenantProductSubscriptions.Add(new TenantProductSubscription
            {
                TenantId = tenantJwt.Id,
                ProductId = product.Id,
                CustomSubdomain = "apex",
                Status = "Active"
            });
            await db.SaveChangesAsync();
        }

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("localhost", 5000);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(PlatformClaimTypes.TenantId, tenantJwt.Id.ToString()),
            new Claim(ClaimTypes.Role, PlatformRoles.TenantAdmin)
        ], "TestAuth"));

        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, db, resolver);

        Assert.Equal(tenantJwt.Id, resolver.CurrentTenantId);
        Assert.Equal("JwtClaim", resolver.Current.ResolutionSource);
    }

    [Fact]
    public async Task GlobalQueryFilters_IsolateWorkoutLogsAndDoorAccessEvents()
    {
        var (db, resolver) = CreateInMemoryContext();
        var productId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (resolver.BeginScope(tenantA, productId))
        {
            var mA = new Member { FirstName = "Jordan", LastName = "A", Email = "ja@test.local" };
            db.Members.Add(mA);
            await db.SaveChangesAsync();
            db.WorkoutLogs.Add(new WorkoutLog { MemberId = mA.Id, Title = "Iron Forge Squats", DurationMinutes = 50 });
            db.DoorAccessEvents.Add(new DoorAccessEvent { MemberId = mA.Id, DoorName = "Main Entrance", Granted = true });
            await db.SaveChangesAsync();
        }

        using (resolver.BeginScope(tenantB, productId))
        {
            var mB = new Member { FirstName = "Devon", LastName = "B", Email = "db@test.local" };
            db.Members.Add(mB);
            await db.SaveChangesAsync();
            db.WorkoutLogs.Add(new WorkoutLog { MemberId = mB.Id, Title = "Apex Rowing", DurationMinutes = 30 });
            db.DoorAccessEvents.Add(new DoorAccessEvent { MemberId = mB.Id, DoorName = "Side Door", Granted = true });
            await db.SaveChangesAsync();
        }

        using (resolver.BeginScope(tenantA, productId))
        {
            var workoutsA = await db.WorkoutLogs.ToListAsync();
            var doorsA = await db.DoorAccessEvents.ToListAsync();
            Assert.Single(workoutsA);
            Assert.Equal("Iron Forge Squats", workoutsA[0].Title);
            Assert.Single(doorsA);
            Assert.Equal("Main Entrance", doorsA[0].DoorName);
        }
    }
}

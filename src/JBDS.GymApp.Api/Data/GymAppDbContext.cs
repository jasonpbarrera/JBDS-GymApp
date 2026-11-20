using System.Linq.Expressions;
using JBDS.GymApp.Api.Data.Entities;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.MultiTenancy;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JBDS.GymApp.Api.Data;

/// <summary>
/// Multi-product, multi-tenant EF Core DbContext backed by Oracle MySQL (MySql.EntityFrameworkCore).
/// Automatically enforces TenantId and ProductId global query filters and write-time tenant stamping
/// using the injected <see cref="ITenantResolverService"/>.
/// </summary>
public sealed class GymAppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITenantResolverService _tenantResolver;

    // Properties evaluated dynamically by EF Core query filters per request/DbContext instance
    private Guid? CurrentTenantId => _tenantResolver.CurrentTenantId;
    private Guid? CurrentProductId => _tenantResolver.CurrentProductId;
    private bool BypassTenantFilter => _tenantResolver.BypassTenantFilter;

    public GymAppDbContext(
        DbContextOptions<GymAppDbContext> options,
        ITenantResolverService tenantResolver)
        : base(options)
    {
        _tenantResolver = tenantResolver;
    }

    // Shared Platform Tables (unfiltered — managed by PlatformAdmin & TenantResolutionMiddleware)
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantProductSubscription> TenantProductSubscriptions => Set<TenantProductSubscription>();

    // Product + Tenant Isolated Gym Tables (auto-filtered by TenantId & ProductId)
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<GymClass> GymClasses => Set<GymClass>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<ClassBooking> ClassBookings => Set<ClassBooking>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<DoorAccessEvent> DoorAccessEvents => Set<DoorAccessEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigurePlatformEntities(builder);
        ConfigureGymEntities(builder);
        ApplyMultiTenantGlobalQueryFilters(builder);
    }

    private static void ConfigurePlatformEntities(ModelBuilder builder)
    {
        builder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.BaseDomain).HasMaxLength(255);
            e.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<Tenant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.CustomDomain).HasMaxLength(255);
            e.Property(x => x.ContactEmail).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.TimeZoneId).HasMaxLength(80);
            e.Property(x => x.PrimaryColor).HasMaxLength(20);
            e.Property(x => x.AccentColor).HasMaxLength(20);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.CustomDomain);
        });

        builder.Entity<TenantProductSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CustomSubdomain).HasMaxLength(100);
            e.Property(x => x.CustomDomain).HasMaxLength(255);
            e.Property(x => x.Tier).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(32);

            e.HasIndex(x => new { x.TenantId, x.ProductId }).IsUnique();
            e.HasIndex(x => x.CustomDomain);
            e.HasIndex(x => x.CustomSubdomain);

            e.HasOne(x => x.Tenant)
                .WithMany(t => t.ProductSubscriptions)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Product)
                .WithMany(p => p.TenantSubscriptions)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
            e.HasIndex(x => new { x.TenantId, x.Email });
        });
    }

    private static void ConfigureGymEntities(ModelBuilder builder)
    {
        builder.Entity<MembershipPlan>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.StripePriceId).HasMaxLength(120);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.IsActive });
        });

        builder.Entity<Member>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.QrCodeToken).HasMaxLength(64).IsRequired();
            e.Property(x => x.BarcodeNumber).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.Email }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.QrCodeToken }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.BarcodeNumber }).IsUnique();
        });

        builder.Entity<MemberSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StripeSubscriptionId).HasMaxLength(120);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.MemberId, x.Status });
            e.HasOne(x => x.Member)
                .WithMany(m => m.Subscriptions)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MembershipPlan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(x => x.MembershipPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CheckIn>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DenialReason).HasMaxLength(250);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.CheckedInAtUtc });
            e.HasOne(x => x.Member)
                .WithMany(m => m.CheckIns)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GymClass>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.TrainerName).HasMaxLength(120);
            e.Property(x => x.LocationRoom).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.IsActive });
        });

        builder.Entity<ClassSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TrainerName).HasMaxLength(120);
            e.Property(x => x.LocationRoom).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.StartTimeUtc });
            e.HasOne(x => x.GymClass)
                .WithMany(c => c.Sessions)
                .HasForeignKey(x => x.GymClassId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClassBooking>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.ClassSessionId, x.MemberId });
            e.HasOne(x => x.ClassSession)
                .WithMany(s => s.Bookings)
                .HasForeignKey(x => x.ClassSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Member)
                .WithMany(m => m.ClassBookings)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.InvoiceNumber).HasMaxLength(64).IsRequired();
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.StripeInvoiceId).HasMaxLength(120);
            e.Property(x => x.StripeCheckoutSessionId).HasMaxLength(150);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.InvoiceNumber }).IsUnique();
            e.HasOne(x => x.Member)
                .WithMany(m => m.Invoices)
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.Property(x => x.StripePaymentIntentId).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(400);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.PaidAtUtc });
            e.HasOne(x => x.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Member)
                .WithMany()
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WorkoutLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(150).IsRequired();
            e.Property(x => x.Category).HasMaxLength(60).IsRequired();
            e.Property(x => x.ExercisesSummary).HasMaxLength(1000);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.MemberId, x.WorkoutDateUtc });
            e.HasOne(x => x.Member)
                .WithMany()
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DoorAccessEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.DoorName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(300);
            e.Property(x => x.ProviderResponse).HasMaxLength(500);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.TriggeredAtUtc });
            e.HasOne(x => x.Member)
                .WithMany()
                .HasForeignKey(x => x.MemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ApplyMultiTenantGlobalQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (typeof(IProductTenantEntity).IsAssignableFrom(clrType))
            {
                var method = typeof(GymAppDbContext)
                    .GetMethod(nameof(SetProductTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, [builder]);
            }
            else if (typeof(ITenantEntity).IsAssignableFrom(clrType) && clrType != typeof(ApplicationUser))
            {
                var method = typeof(GymAppDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(clrType);
                method.Invoke(this, [builder]);
            }
        }
    }

    private void SetProductTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, IProductTenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            BypassTenantFilter ||
            (CurrentTenantId != null && e.TenantId == CurrentTenantId &&
             (CurrentProductId == null || e.ProductId == CurrentProductId)));
    }

    private void SetTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            BypassTenantFilter ||
            (CurrentTenantId != null && e.TenantId == CurrentTenantId));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceMultiTenantWriteRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceMultiTenantWriteRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceMultiTenantWriteRules()
    {
        if (BypassTenantFilter) return;

        var tenantId = CurrentTenantId;
        var productId = CurrentProductId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.Entity is ApplicationUser && entry.Entity.TenantId != Guid.Empty && tenantId is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                if (tenantId is null || tenantId == Guid.Empty)
                {
                    throw new InvalidOperationException(
                        $"Cannot insert tenant-scoped entity '{entry.Entity.GetType().Name}' without a resolved TenantId.");
                }

                if (entry.Entity.TenantId != Guid.Empty && entry.Entity.TenantId != tenantId.Value)
                {
                    throw new UnauthorizedAccessException(
                        $"Cross-tenant write blocked: entity TenantId {entry.Entity.TenantId} does not match active TenantId {tenantId.Value}.");
                }

                entry.Entity.TenantId = tenantId.Value;

                if (entry.Entity is IProductTenantEntity productEntity)
                {
                    if (productId is null || productId == Guid.Empty)
                    {
                        throw new InvalidOperationException(
                            $"Cannot insert product-scoped entity '{entry.Entity.GetType().Name}' without a resolved ProductId.");
                    }

                    if (productEntity.ProductId != Guid.Empty && productEntity.ProductId != productId.Value)
                    {
                        throw new UnauthorizedAccessException(
                            $"Cross-product write blocked: entity ProductId {productEntity.ProductId} does not match active ProductId {productId.Value}.");
                    }

                    productEntity.ProductId = productId.Value;
                }
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (tenantId is not null && entry.Entity.TenantId != tenantId.Value)
                {
                    throw new UnauthorizedAccessException(
                        $"Cross-tenant modification blocked for '{entry.Entity.GetType().Name}'.");
                }

                if (entry.Entity is IProductTenantEntity productEntity &&
                    productId is not null &&
                    productEntity.ProductId != productId.Value)
                {
                    throw new UnauthorizedAccessException(
                        $"Cross-product modification blocked for '{entry.Entity.GetType().Name}'.");
                }
            }
        }
    }
}

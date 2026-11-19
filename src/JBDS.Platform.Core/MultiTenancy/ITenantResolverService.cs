namespace JBDS.Platform.Core.MultiTenancy;

/// <summary>
/// Scoped service injected into EF Core DbContext and application services to provide
/// the active TenantId and ProductId for global query filters and write enforcement.
/// </summary>
public interface ITenantResolverService
{
    TenantContext Current { get; }
    Guid? CurrentTenantId { get; }
    Guid? CurrentProductId { get; }
    bool BypassTenantFilter { get; }

    /// <summary>
    /// Sets the active context for the current request/scope.
    /// </summary>
    void SetContext(TenantContext context);

    /// <summary>
    /// Temporarily overrides the active Tenant/Product context until disposed.
    /// </summary>
    IDisposable BeginScope(TenantContext context);

    /// <summary>
    /// Temporarily switches to a specific TenantId and optional ProductId until disposed.
    /// </summary>
    IDisposable BeginScope(Guid tenantId, Guid? productId = null, string source = "Scope");

    /// <summary>
    /// Temporarily bypasses EF Core global tenant/product filters (e.g., for Platform Admin, login, or seeding).
    /// </summary>
    IDisposable BeginUnrestrictedScope();
}

namespace JBDS.Platform.Core.MultiTenancy;

/// <summary>
/// Default scoped implementation of <see cref="ITenantResolverService"/>.
/// Registered as Scoped in DI so each HTTP request / DbContext shares the same resolved tenant context.
/// </summary>
public sealed class TenantResolverService : ITenantResolverService
{
    private TenantContext _current = TenantContext.Empty;

    public TenantContext Current => _current;
    public Guid? CurrentTenantId => _current.TenantId;
    public Guid? CurrentProductId => _current.ProductId;
    public bool BypassTenantFilter => _current.BypassTenantFilter;

    public void SetContext(TenantContext context)
    {
        _current = context ?? TenantContext.Empty;
    }

    public IDisposable BeginScope(TenantContext context)
    {
        var previous = _current;
        _current = context ?? TenantContext.Empty;
        return new ScopeReset(this, previous);
    }

    public IDisposable BeginScope(Guid tenantId, Guid? productId = null, string source = "Scope")
    {
        return BeginScope(new TenantContext
        {
            TenantId = tenantId,
            ProductId = productId,
            ResolutionSource = source,
            BypassTenantFilter = false
        });
    }

    public IDisposable BeginUnrestrictedScope()
    {
        return BeginScope(_current with { BypassTenantFilter = true, ResolutionSource = "UnrestrictedScope" });
    }

    private sealed class ScopeReset(TenantResolverService owner, TenantContext previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            owner._current = previous;
            _disposed = true;
        }
    }
}

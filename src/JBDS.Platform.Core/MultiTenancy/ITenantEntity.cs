namespace JBDS.Platform.Core.MultiTenancy;

/// <summary>
/// Marks an EF Core entity as isolated by a TenantId discriminator column.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

/// <summary>
/// Marks an EF Core entity as isolated by both TenantId and ProductId discriminator columns,
/// allowing a shared Tenant to subscribe to multiple Products in the same database without data overlap.
/// </summary>
public interface IProductTenantEntity : ITenantEntity
{
    Guid ProductId { get; set; }
}

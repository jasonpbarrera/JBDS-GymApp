using JBDS.Platform.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JBDS.GymApp.Api.Data;

/// <summary>
/// Design-time factory for `dotnet ef migrations add` targeting Oracle MySQL (MySql.EntityFrameworkCore).
/// </summary>
public sealed class GymAppDbContextFactory : IDesignTimeDbContextFactory<GymAppDbContext>
{
    public GymAppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                   ?? "Server=127.0.0.1;Port=3306;Database=jbds_platform;User=jbds_app;Password=ChangeMe_App_2026!;SslMode=Preferred;";

        var optionsBuilder = new DbContextOptionsBuilder<GymAppDbContext>();
        optionsBuilder.UseMySQL(conn);

        return new GymAppDbContext(optionsBuilder.Options, new TenantResolverService());
    }
}

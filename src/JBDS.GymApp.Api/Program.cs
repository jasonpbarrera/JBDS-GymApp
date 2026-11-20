using System.Text;
using JBDS.GymApp.Api.Data;
using JBDS.GymApp.Api.Middleware;
using JBDS.GymApp.Api.Services;
using JBDS.Platform.Core.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Multi-Tenant & Multi-Product Resolver Service (Scoped per HTTP request / EF Core DbContext)
builder.Services.AddScoped<ITenantResolverService, TenantResolverService>();

// 2. Database Provider: Oracle MySQL (MySql.EntityFrameworkCore 10.x) or InMemory for testing
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = builder.Configuration.GetValue<bool>("Database:UseInMemory");

builder.Services.AddDbContext<GymAppDbContext>(options =>
{
    if (useInMemory || string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseInMemoryDatabase("JBDS_GymApp_Dev");
    }
    else
    {
        options.UseMySQL(connectionString);
    }
});

// 3. ASP.NET Core Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<GymAppDbContext>()
    .AddDefaultTokenProviders();

// 4. JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? "JBDS_GYMAPP_DEV_SIGNING_KEY_CHANGE_IN_PRODUCTION_2026_MIN_32_CHARS!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "JBDS.Platform";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "JBDS.GymApp";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// 5. Application Services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<StripeBillingService>();

// 6. Forwarded headers (preserves original Host/Proto behind Firebase Hosting -> Cloud Run rewrites)
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 7. CORS for Firebase Hosting & local Blazor WASM dev
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5002", "https://localhost:7002", "https://*.web.app", "https://*.firebaseapp.com", "https://*.jbdigitalservices.net"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("GymAppCors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Resolved-Tenant", "X-Resolved-Product", "X-Tenant-Resolution-Source");
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("GymAppCors");
app.UseAuthentication();

// Resolve Tenant + Product AFTER Authentication so JWT claims are available if domain/subdomain did not match
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();
app.MapControllers();

// Auto-create schema & seed demo multi-product/multi-tenant data when enabled
if (app.Configuration.GetValue("Platform:AutoMigrateAndSeed", true))
{
    await DbSeeder.SeedAsync(app.Services);
}

app.Run();

public partial class Program;

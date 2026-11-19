using JBDS.GymApp.Client;
using JBDS.GymApp.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApiBase = builder.Configuration["ApiBaseUrl"];
var apiBaseUri = string.IsNullOrWhiteSpace(configuredApiBase)
    ? new Uri(builder.HostEnvironment.BaseAddress)
    : new Uri(configuredApiBase);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiBaseUri });
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<GymAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<GymAuthStateProvider>());
builder.Services.AddScoped<GymApiClient>();

await builder.Build().RunAsync();

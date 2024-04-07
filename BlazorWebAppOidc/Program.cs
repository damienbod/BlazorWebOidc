using Microsoft.AspNetCore.Components.Authorization;
using BlazorWebAppOidc;
using BlazorWebAppOidc.Components;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using BlazorWebAppOidc.Weather;
using BlazorWebAppOidc.Client.Weather;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using BlazorWebAppOidc.CspServices;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;

const string OIDC_SCHEME = "MicrosoftOidc";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthentication(OIDC_SCHEME)
    .AddOpenIdConnect(OIDC_SCHEME, options =>
    {
        // From appsettings.json, keyvault, user-secrets
        // "OpenIDConnectSettings": {
        //  "Authority": "https://localhost:44318",
        //  "ClientId": "oidc-pkce-confidential",
        //  "ClientSecret": "--secret-in-key-vault-user-secrets--"
        // },
        builder.Configuration.GetSection("OpenIDConnectSettings").Bind(options);

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.ResponseType = OpenIdConnectResponseType.Code;

        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false; // Remove Microsoft mappings
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name"
        };
    })
    .AddCookie();

// ConfigureCookieOidcRefresh attaches a cookie OnValidatePrincipal callback to get
// a new access token when the current one expires, and reissue a cookie with the
// new access token saved inside. If the refresh fails, the user will be signed
// out. OIDC connect options are set for saving tokens and the offline access
// scope.
builder.Services.ConfigureCookieOidcRefresh(CookieAuthenticationDefaults.AuthenticationScheme, OIDC_SCHEME);

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<CircuitHandler, BlazorNonceService>(sp =>
     sp.GetRequiredService<BlazorNonceService>()));

builder.Services.AddScoped<BlazorNonceService>();

builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();

builder.Services.AddScoped<IWeatherForecaster, ServerWeatherForecaster>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

//JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseSecurityHeaders(
    SecurityHeadersDefinitions.GetHeaderPolicyCollection(app.Environment.IsDevelopment(),
        app.Configuration["OpenIDConnectSettings:Authority"]));

app.UseMiddleware<NonceMiddleware>();

app.UseStaticFiles();
app.UseAntiforgery();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weather-forecast", ([FromServices] IWeatherForecaster WeatherForecaster) =>
{
    return WeatherForecaster.GetWeatherForecastAsync();
}).RequireAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorWebAppOidc.Client._Imports).Assembly);

app.MapGroup("/authentication").MapLoginAndLogout();

app.Run();

using BlazorWebAppOidc;
using BlazorWebAppOidc.Client.Weather;
using BlazorWebAppOidc.Components;
using BlazorWebAppOidc.CspServices;
using BlazorWebAppOidc.Weather;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;

const string OIDC_SCHEME = "MicrosoftOidc";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSecurityHeaderPolicies()
  .SetPolicySelector((PolicySelectorContext ctx) =>
  {
      return SecurityHeadersDefinitions.GetHeaderPolicyCollection(
          builder.Environment.IsDevelopment(),
          builder.Configuration["OpenIDConnectSettings:Authority"]);
  });


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
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IWeatherForecaster, ServerWeatherForecaster>();

var app = builder.Build();

//JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseSecurityHeaders();

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
    .AddAdditionalAssemblies(typeof(BlazorWebAppOidc.Client._Imports).Assembly);

app.MapGroup("/authentication").MapLoginAndLogout();

app.Run();

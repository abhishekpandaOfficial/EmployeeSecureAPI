using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Load Azure Key Vault (secrets override appsettings.json)
var keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"];
if (!string.IsNullOrWhiteSpace(keyVaultUrl))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential()  // Managed Identity in Azure, or developer identity locally
    );
}

// 2️⃣ Add services
builder.Services.AddControllers();

// 🔐 Authentication (JWT + Microsoft Identity)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// 📖 Swagger with OAuth2
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Employee Secure API with OAuth2.0",
        Version = "v1"
    });

    var tenantId = builder.Configuration["AzureAd:TenantId"];
    var audience = builder.Configuration["AzureAd:Audience"];
    var swaggerAuthUrl = builder.Configuration["SwaggerOAuth:AuthorizationUrl"];
    var swaggerTokenUrl = builder.Configuration["SwaggerOAuth:TokenUrl"];
    var swaggerScope = builder.Configuration["SwaggerOAuth:Scopes"];

    // OAuth2 Flow
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(swaggerAuthUrl, UriKind.Absolute),
                TokenUrl = new Uri(swaggerTokenUrl, UriKind.Absolute),
                Scopes = new Dictionary<string, string>
                {
                    { swaggerScope, "Access Employee API" }
                }
            }
        }
    });

    // Requirement: API requires OAuth2
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { $"{audience}/Employee.Read" }
        }
    });
});

var app = builder.Build();

// 3️⃣ Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "EmployeeSecureAPI v1");

        // 👇 Must match the ClientId of your Swagger OAuth SPA App Registration
        o.OAuthClientId(builder.Configuration["SwaggerOAuth:ClientId"]);
        o.OAuthUsePkce(); // No secret needed
        o.OAuthScopes($"{builder.Configuration["AzureAd:Audience"]}/Employee.Read");
        o.OAuthAppName("Swagger UI for Employee API");
    });
}

app.UseHttpsRedirection();

// Debugging: Print JWT Claims if token is present
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var token = authHeader.ToString().Replace("Bearer ", "");
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            Console.WriteLine("🔎 JWT Claims:");
            foreach (var claim in jwtToken.Claims)
            {
                Console.WriteLine($"{claim.Type}: {claim.Value}");
            }
        }
        catch
        {
            Console.WriteLine("⚠️ Invalid or no JWT token found");
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

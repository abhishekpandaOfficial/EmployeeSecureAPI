using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// 🔐 Authentication & Authorization (API)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// 📖 Swagger + OAuth2 config
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
    var authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize";
    var tokenUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{builder.Configuration["SwaggerOAuth:AuthorizationUrl"]}", UriKind.Absolute),
                TokenUrl = new Uri($"{builder.Configuration["SwaggerOAuth:TokenUrl"]}", UriKind.Absolute),
            Scopes = new Dictionary<string, string>
            {
                { builder.Configuration["SwaggerOAuth:Scopes"], "Access Employee API" }
            }
            }
        }
    });

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

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "EmployeeSecureAPI v1");

        // 👇 This ClientId must be from your Swagger OAuth SPA app registration
        o.OAuthClientId(builder.Configuration["SwaggerOAuth:ClientId"]);
        o.OAuthUsePkce();  // that tells Swagger UI not to ask for a client secret.
        o.OAuthScopes($"{builder.Configuration["AzureAd:Audience"]}/Employee.Read");
        o.OAuthAppName("Swagger UI for Employee API");
    });
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var token = authHeader.ToString().Replace("Bearer ", "");
        
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Console.WriteLine("🔎 JWT Claims:");
        foreach (var claim in jwtToken.Claims)
        {
            Console.WriteLine($"{claim.Type}: {claim.Value}");
        }
    }

    await next();
});


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

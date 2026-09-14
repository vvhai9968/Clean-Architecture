using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Platform.APIs;
using Platform.APIs.Application.MigrateData;
using Platform.APIs.Endpoints.Auth;
using Platform.APIs.Endpoints.Tvan;
using Platform.DotnetExtensions;
using Platform.Infrastructure.Persistence.PlatformContext;
using Platform.Shared;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var environment = builder.Environment;

services.SetupEnvs(environment, out AppSettings appSettings);
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        }
    });
});

services.AddHttpContextAccessor();
services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(Platform.Application.Assembly).Assembly));
services.AddService();
services.AddDbContext(appSettings);
services.AddHttpClient();

var jwt = appSettings.Jwt;
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
    });
services.AddAuthorization();

services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        if (appSettings.CorsOrigins.Length > 0)
        {
            policy.WithOrigins(appSettings.CorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseCors("AllowAll");
app.MigrateDatabase<PlatformDbContext>((_, _) => { });

app.UseAuthentication();
app.UseAuthorization();

if (environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

AuthEndPoint.APIs(app);
TvanEndPoint.APIs(app);

app.MapGet("api/me", (HttpContext http) =>
{
    var user = http.User;
    return Results.Ok(new
    {
        Id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
             ?? user.FindFirst("sub")?.Value,
        Email = user.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value
                ?? user.FindFirst("email")?.Value,
        Roles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray(),
    });
}).RequireAuthorization();

await DataSeed.SeedAsync(app.Services);
await TvanSeed.SeedAsync(app.Services, environment);
app.Run();

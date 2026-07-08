using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PorodicnoStablo.Api.Auth;
using PorodicnoStablo.Api.Data;
using PorodicnoStablo.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var supabaseUrl = (cfg["Supabase:Url"] ?? throw new InvalidOperationException("Supabase:Url nije podešen")).TrimEnd('/');
var jwtIssuer = $"{supabaseUrl}/auth/v1";

// ── Baza (postojeći Supabase Postgres) ───────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cfg.GetConnectionString("Db")));

// ── JSON: snake_case da JSON ugovor ostane identičan supabase-js odgovorima ──
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

// ── Autentikacija: validacija Supabase JWT tokena ────────────────────────────
// Podržana su oba načina:
//  1) Supabase:JwtSecret (legacy HS256 "JWT Secret" iz dashboarda) — preporučeno, najjednostavnije
//  2) bez secreta — povlači JWKS (novi asimetrični ključevi) sa /auth/v1/.well-known/jwks.json
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudiences = ["authenticated"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        var jwtSecret = cfg["Supabase:JwtSecret"];
        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            o.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        }
        else
        {
            using var http = new HttpClient();
            var jwks = http.GetStringAsync($"{jwtIssuer}/.well-known/jwks.json").GetAwaiter().GetResult();
            o.TokenValidationParameters.IssuerSigningKeys = new JsonWebKeySet(jwks).GetSigningKeys();
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("admin"));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IClaimsTransformation, ProfileClaimsTransformation>();
builder.Services.AddHttpClient<SupabaseStorageService>();

// ── CORS ─────────────────────────────────────────────────────────────────────
var origins = cfg.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ── Swagger sa JWT podrškom ──────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "PorodicnoStablo API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Supabase access token. Unesi: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

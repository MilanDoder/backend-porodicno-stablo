using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PorodicnoStablo.Api.Auth;
using PorodicnoStablo.Api.Data;
using PorodicnoStablo.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ── Render (i slični PaaS) ubacuju PORT env var i očekuju da server sluša na njemu ──
var renderPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}

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
            // Ne fetch-ujemo JWKS samo jednom na startu (to zamrzava ključeve zauvek) —
            // resolver se poziva pri validaciji, sa kešom od 10 min da ne bombardujemo Supabase.
            var jwksCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            const string jwksKey = "supabase-jwks";

            o.TokenValidationParameters.IssuerSigningKeyResolver = (t, securityToken, kid, parameters) =>
            {
                if (jwksCache.TryGetValue(jwksKey, out IEnumerable<SecurityKey>? cached) && cached is not null)
                    return cached;

                using var http = new HttpClient();
                var jwks = http.GetStringAsync($"{jwtIssuer}/.well-known/jwks.json").GetAwaiter().GetResult();
                var keys = new JsonWebKeySet(jwks).GetSigningKeys();
                jwksCache.Set(jwksKey, keys, TimeSpan.FromMinutes(10));
                return keys;
            };
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("admin"));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IClaimsTransformation, ProfileClaimsTransformation>();
builder.Services.AddHttpClient<SupabaseStorageService>();

// ── Forwarded headers: Render (i drugi PaaS) stoje iza reverse proxy-ja ─────
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Proxy Rendera nije statičan po IP-ju, pa praznimo liste da middleware ne odbija zaglavlja
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

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

app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

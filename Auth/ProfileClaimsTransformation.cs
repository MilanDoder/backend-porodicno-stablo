using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PorodicnoStablo.Api.Data;

namespace PorodicnoStablo.Api.Auth;

/// <summary>
/// Nakon validacije Supabase JWT-a dodaje ClaimTypes.Role iz tabele profiles.
/// Autorizacija je time SERVERSKA — klijent više ne odlučuje ko je admin.
/// Rola se kešira 2 minuta da se ne gađa baza na svaki zahtjev.
/// </summary>
public class ProfileClaimsTransformation(IServiceProvider services, IMemoryCache cache) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is not { IsAuthenticated: true }) return principal;
        if (identity.HasClaim(c => c.Type == ClaimTypes.Role)) return principal;

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId)) return principal;

        var role = await cache.GetOrCreateAsync($"role:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == userId);
            return profile?.Role ?? "user";
        });

        identity.AddClaim(new Claim(ClaimTypes.Role, role ?? "user"));
        return principal;
    }
}

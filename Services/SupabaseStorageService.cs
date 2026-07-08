namespace PorodicnoStablo.Api.Services;

/// <summary>
/// Tanki klijent za Supabase Storage REST API — koristi service_role ključ,
/// tako da RLS/policy na bucket-u više ne mora biti otvoren za anon klijente.
/// </summary>
public class SupabaseStorageService(HttpClient http, IConfiguration cfg)
{
    private readonly string _baseUrl = (cfg["Supabase:Url"] ?? "").TrimEnd('/');
    private readonly string _serviceKey = cfg["Supabase:ServiceRoleKey"] ?? "";

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{_baseUrl}/storage/v1/{path}");
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_serviceKey}");
        req.Headers.TryAddWithoutValidation("apikey", _serviceKey);
        return req;
    }

    public async Task UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var req = NewRequest(HttpMethod.Post, $"object/{bucket}/{Uri.EscapeDataString(path)}");
        req.Content = new StreamContent(content);
        req.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Storage upload nije uspio ({(int)res.StatusCode}): {await res.Content.ReadAsStringAsync(ct)}");
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        var req = NewRequest(HttpMethod.Delete, $"object/{bucket}/{Uri.EscapeDataString(path)}");
        var res = await http.SendAsync(req, ct);
        // 404 tolerišemo — fajl je već obrisan
        if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException($"Storage delete nije uspio ({(int)res.StatusCode}): {await res.Content.ReadAsStringAsync(ct)}");
    }

    public string? GetPublicUrl(string bucket, string? path)
        => string.IsNullOrEmpty(path) ? null : $"{_baseUrl}/storage/v1/object/public/{bucket}/{path}";
}

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using YaalTrail.Api.Configuration;

namespace YaalTrail.Api.Services;

// FR-45: where place photos are kept. Only the public URL is stored in the database.
public interface IPhotoStorage
{
    // Saves the file under a new random name and returns its public URL.
    Task<string> UploadAsync(byte[] content, ImageType type);

    // Removes the file behind a URL that UploadAsync returned.
    Task DeleteAsync(string publicUrl);
}

// The storage service failed or is not configured. The controller returns 502.
public class PhotoStorageException : Exception
{
    public PhotoStorageException(string message) : base(message)
    {
    }
}

// Supabase Storage REST API, called with HttpClient:
//   upload: POST   {Url}/storage/v1/object/{bucket}/{name}
//   delete: DELETE {Url}/storage/v1/object/{bucket}/{name}
//   public: GET    {Url}/storage/v1/object/public/{bucket}/{name}
public class SupabasePhotoStorage : IPhotoStorage
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabasePhotoStorage> _logger;

    public SupabasePhotoStorage(HttpClient http, IOptions<SupabaseOptions> options, ILogger<SupabasePhotoStorage> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    private string BaseUrl => _options.Url.TrimEnd('/');
    private string PublicPrefix => $"{BaseUrl}/storage/v1/object/public/{_options.Bucket}/";

    public async Task<string> UploadAsync(byte[] content, ImageType type)
    {
        EnsureConfigured();

        // A random name: nothing about the original file name ends up in the URL.
        string objectName = Guid.NewGuid().ToString("N") + type.Extension;

        using var request = NewRequest(HttpMethod.Post, objectName);
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(type.ContentType);
        request.Headers.Add("x-upsert", "false");

        using HttpResponseMessage response = await SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            // NFR-15: only the status code is logged, never the key or the request headers.
            _logger.LogWarning("Photo upload failed with status {Status}", (int)response.StatusCode);
            throw new PhotoStorageException("The photo could not be saved to storage. Please try again.");
        }

        return PublicPrefix + objectName;
    }

    public async Task DeleteAsync(string publicUrl)
    {
        EnsureConfigured();

        // Only files in our own bucket are deleted.
        if (!publicUrl.StartsWith(PublicPrefix, StringComparison.Ordinal))
            return;

        string objectName = publicUrl[PublicPrefix.Length..];
        using var request = NewRequest(HttpMethod.Delete, objectName);
        using HttpResponseMessage response = await SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Photo delete failed with status {Status}", (int)response.StatusCode);
            throw new PhotoStorageException("The photo file could not be removed from storage.");
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string objectName)
    {
        var request = new HttpRequestMessage(method,
            $"{BaseUrl}/storage/v1/object/{_options.Bucket}/{Uri.EscapeDataString(objectName)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceKey);
        request.Headers.Add("apikey", _options.ServiceKey);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        try
        {
            return await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Photo storage could not be reached: {Error}", ex.GetType().Name);
            throw new PhotoStorageException("Photo storage could not be reached. Please try again.");
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Url) || string.IsNullOrWhiteSpace(_options.ServiceKey))
            throw new PhotoStorageException("Photo storage is not configured (Supabase:Url and Supabase:ServiceKey).");
    }
}

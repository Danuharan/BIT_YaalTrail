namespace YaalTrail.Api.Configuration;

// FR-45: place photos are stored in Supabase Storage, not in the database.
// NFR-15: Url and ServiceKey come only from user-secrets or environment variables
// (Supabase__Url, Supabase__ServiceKey). The key is never logged.
public class SupabaseOptions
{
    public const string SectionName = "Supabase";

    // e.g. https://<project>.supabase.co
    public string Url { get; set; } = string.Empty;
    public string ServiceKey { get; set; } = string.Empty;

    // A PUBLIC bucket, so visitors' browsers can load the photos directly.
    public string Bucket { get; set; } = "place-photos";
}

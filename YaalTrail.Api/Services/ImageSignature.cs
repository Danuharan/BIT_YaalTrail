namespace YaalTrail.Api.Services;

// The kind of image found from the file's first bytes.
public record ImageType(string ContentType, string Extension);

// FR-45: an upload is accepted because of what the file really contains (its
// "magic bytes"), not because of its name or the Content-Type the browser sent.
// Pure logic, no I/O, so it can be unit tested (NFR-23).
public static class ImageSignature
{
    // Enough bytes to recognise all three formats.
    public const int HeaderLength = 12;

    public static readonly ImageType Jpeg = new("image/jpeg", ".jpg");
    public static readonly ImageType Png = new("image/png", ".png");
    public static readonly ImageType Webp = new("image/webp", ".webp");

    private static readonly byte[] JpegStart = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] WebpMark = "WEBP"u8.ToArray();

    // Returns null for anything that is not JPEG, PNG or WebP.
    public static ImageType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegStart))
            return Jpeg;

        if (header.StartsWith(PngStart))
            return Png;

        // WebP: "RIFF", 4 bytes of file size, then "WEBP".
        if (header.Length >= HeaderLength && header.StartsWith(Riff) && header.Slice(8, 4).SequenceEqual(WebpMark))
            return Webp;

        return null;
    }
}

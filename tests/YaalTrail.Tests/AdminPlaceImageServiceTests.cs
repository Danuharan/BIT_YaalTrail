using Microsoft.Extensions.Logging.Abstractions;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// Stands in for Supabase Storage: remembers uploads and deletes, and can be told to fail.
public class FakePhotoStorage : IPhotoStorage
{
    public List<(byte[] Content, ImageType Type, string Url)> Uploads { get; } = new();
    public List<string> Deleted { get; } = new();
    public bool FailUploads { get; set; }
    public bool FailDeletes { get; set; }

    public Task<string> UploadAsync(byte[] content, ImageType type)
    {
        if (FailUploads) throw new PhotoStorageException("Storage is down.");
        string url = $"https://storage.test/place-photos/{Guid.NewGuid():N}{type.Extension}";
        Uploads.Add((content, type, url));
        return Task.FromResult(url);
    }

    public Task DeleteAsync(string publicUrl)
    {
        if (FailDeletes) throw new PhotoStorageException("Storage is down.");
        Deleted.Add(publicUrl);
        return Task.CompletedTask;
    }
}

// FR-45: file signature checks.
public class ImageSignatureTests
{
    public static byte[] Jpeg(int size = 100) => WithHeader(size, 0xFF, 0xD8, 0xFF, 0xE0);
    public static byte[] Png(int size = 100) => WithHeader(size, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    public static byte[] Webp(int size = 100) => WithHeader(size, (byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4,
        (byte)'W', (byte)'E', (byte)'B', (byte)'P');

    private static byte[] WithHeader(int size, params byte[] header)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    [Fact]
    public void Detect_JpegBytes_ReturnsJpeg() => Assert.Equal(ImageSignature.Jpeg, ImageSignature.Detect(Jpeg()));

    [Fact]
    public void Detect_PngBytes_ReturnsPng() => Assert.Equal(ImageSignature.Png, ImageSignature.Detect(Png()));

    [Fact]
    public void Detect_WebpBytes_ReturnsWebp() => Assert.Equal(ImageSignature.Webp, ImageSignature.Detect(Webp()));

    public static TheoryData<byte[]> NotImages => new()
    {
        "GIF89a......"u8.ToArray(),                          // a GIF
        "%PDF-1.7 ...."u8.ToArray(),                         // a PDF
        "<html><body>"u8.ToArray(),                          // text named photo.jpg
        "RIFF\0\0\0\0WAVE"u8.ToArray(),                      // RIFF but audio, not WebP
        new byte[] { 0xFF, 0xD8 },                           // cut-off JPEG header
        Array.Empty<byte>(),
    };

    [Theory]
    [MemberData(nameof(NotImages))]
    public void Detect_OtherContent_ReturnsNull(byte[] bytes)
    {
        Assert.Null(ImageSignature.Detect(bytes));
    }
}

// FR-11, FR-45, FR-48 with the in-memory database and the fake storage.
public class AdminPlaceImageServiceTests
{
    private readonly AppDbContext _db = TestData.NewDatabase();
    private readonly FakePhotoStorage _storage = new();
    private readonly SpyCatalogueCache _cache = new();
    private readonly FakeClock _clock = FakeClock.MondayMorning();
    private readonly AdminPlaceImageService _service;
    private readonly Place _place;
    private const int AdminId = 7;

    public AdminPlaceImageServiceTests()
    {
        _service = new AdminPlaceImageService(_db, _storage, _cache, _clock, NullLogger<AdminPlaceImageService>.Instance);
        _place = new Place { Name = "Jaffna Fort", Category = new Category { Name = "Heritage" } };
        _db.Places.Add(_place);
        _db.SaveChanges();
    }

    private Task<AdminPlaceImageDto?> Upload(byte[] bytes, string? caption = null, long? length = null) =>
        _service.AddAsync(_place.Id, new MemoryStream(bytes), length ?? bytes.Length, caption, AdminId);

    // ---------- upload ----------

    [Fact]
    public async Task AddAsync_ValidJpeg_StoresFileAndSavesOnlyItsUrl()
    {
        AdminPlaceImageDto? image = await Upload(ImageSignatureTests.Jpeg(), "  The gate  ");

        var upload = Assert.Single(_storage.Uploads);
        Assert.Equal("image/jpeg", upload.Type.ContentType);
        Assert.Equal(upload.Url, image!.ImageUrl);
        Assert.Equal("The gate", image.Caption);
        Assert.Equal(0, image.SortOrder);
        Assert.Equal(upload.Url, Assert.Single(_db.PlaceImages).ImageUrl);
    }

    [Fact]
    public async Task AddAsync_ValidPhoto_InvalidatesCacheAndRecordsAdmin()
    {
        _clock.Advance(TimeSpan.FromMinutes(5));

        await Upload(ImageSignatureTests.Png());

        Assert.Equal(1, _cache.InvalidateCount);
        Assert.Equal(AdminId, _place.MaintainedByAdminId);
        Assert.Equal(_clock.UtcNow, _place.UpdatedAt);
    }

    [Fact]
    public async Task AddAsync_SecondPhoto_GoesToTheEnd()
    {
        await Upload(ImageSignatureTests.Jpeg());
        AdminPlaceImageDto? second = await Upload(ImageSignatureTests.Webp());

        Assert.Equal(1, second!.SortOrder);
    }

    // A text file renamed to .jpg is still refused: the content is checked, not the name.
    [Fact]
    public async Task AddAsync_FileThatIsNotAnImage_ThrowsAndUploadsNothing()
    {
        var error = await Assert.ThrowsAsync<AdminRequestException>(() => Upload("<html>not a photo</html>"u8.ToArray()));

        Assert.Contains("JPEG, PNG or WebP", error.Message);
        Assert.Empty(_storage.Uploads);
        Assert.Equal(0, _cache.InvalidateCount);
    }

    [Fact]
    public async Task AddAsync_FileOver3Mb_Throws()
    {
        byte[] big = ImageSignatureTests.Jpeg((int)AdminPlaceImageService.MaxFileBytes + 1);

        var error = await Assert.ThrowsAsync<AdminRequestException>(() => Upload(big));

        Assert.Contains("3 MB", error.Message);
        Assert.Empty(_storage.Uploads);
    }

    // The reported length cannot be trusted: reading stops once more than 3 MB has arrived.
    [Fact]
    public async Task AddAsync_LargeFileWithSmallReportedLength_Throws()
    {
        byte[] big = ImageSignatureTests.Jpeg((int)AdminPlaceImageService.MaxFileBytes + 1);

        await Assert.ThrowsAsync<AdminRequestException>(() => Upload(big, length: 1000));
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task AddAsync_FileExactly3Mb_IsAccepted()
    {
        AdminPlaceImageDto? image = await Upload(ImageSignatureTests.Jpeg((int)AdminPlaceImageService.MaxFileBytes));

        Assert.NotNull(image);
    }

    [Fact]
    public async Task AddAsync_EmptyFile_Throws()
    {
        await Assert.ThrowsAsync<AdminRequestException>(() => Upload(Array.Empty<byte>()));
    }

    [Fact]
    public async Task AddAsync_SixthPhoto_Throws()
    {
        for (int i = 0; i < 5; i++) await Upload(ImageSignatureTests.Jpeg());

        var error = await Assert.ThrowsAsync<AdminRequestException>(() => Upload(ImageSignatureTests.Jpeg()));

        Assert.Contains("at most 5 photos", error.Message);
        Assert.Equal(5, _storage.Uploads.Count);
    }

    [Fact]
    public async Task AddAsync_CaptionLongerThan200_Throws()
    {
        await Assert.ThrowsAsync<AdminRequestException>(() => Upload(ImageSignatureTests.Jpeg(), new string('x', 201)));
    }

    [Fact]
    public async Task AddAsync_UnknownPlace_ReturnsNull()
    {
        var bytes = ImageSignatureTests.Jpeg();

        Assert.Null(await _service.AddAsync(999, new MemoryStream(bytes), bytes.Length, null, AdminId));
        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task AddAsync_StorageFails_ThrowsAndSavesNoRow()
    {
        _storage.FailUploads = true;

        await Assert.ThrowsAsync<PhotoStorageException>(() => Upload(ImageSignatureTests.Jpeg()));

        Assert.Empty(_db.PlaceImages);
        Assert.Equal(0, _cache.InvalidateCount);
    }

    // ---------- reorder ----------

    [Fact]
    public async Task ReorderAsync_AllIdsInNewOrder_SavesThatOrder()
    {
        int a = (await Upload(ImageSignatureTests.Jpeg()))!.Id;
        int b = (await Upload(ImageSignatureTests.Png()))!.Id;
        int c = (await Upload(ImageSignatureTests.Webp()))!.Id;

        List<AdminPlaceImageDto> result = (await _service.ReorderAsync(_place.Id, new List<int> { c, a, b }, AdminId))!;

        Assert.Equal(new[] { c, a, b }, result.Select(i => i.Id));
        Assert.Equal(new[] { 0, 1, 2 }, result.Select(i => i.SortOrder));
        Assert.Equal(4, _cache.InvalidateCount); // 3 uploads + 1 reorder
    }

    [Fact]
    public async Task ReorderAsync_MissingOrForeignId_Throws()
    {
        int a = (await Upload(ImageSignatureTests.Jpeg()))!.Id;
        await Upload(ImageSignatureTests.Png());

        await Assert.ThrowsAsync<AdminRequestException>(() => _service.ReorderAsync(_place.Id, new List<int> { a }, AdminId));
        await Assert.ThrowsAsync<AdminRequestException>(() => _service.ReorderAsync(_place.Id, new List<int> { a, a }, AdminId));
        await Assert.ThrowsAsync<AdminRequestException>(() => _service.ReorderAsync(_place.Id, new List<int> { a, 999 }, AdminId));
    }

    [Fact]
    public async Task ReorderAsync_UnknownPlace_ReturnsNull()
    {
        Assert.Null(await _service.ReorderAsync(999, new List<int>(), AdminId));
    }

    // ---------- delete ----------

    [Fact]
    public async Task DeleteAsync_ExistingPhoto_RemovesRowAndFileAndRenumbers()
    {
        AdminPlaceImageDto first = (await Upload(ImageSignatureTests.Jpeg()))!;
        AdminPlaceImageDto second = (await Upload(ImageSignatureTests.Png()))!;

        bool deleted = await _service.DeleteAsync(_place.Id, first.Id, AdminId);

        Assert.True(deleted);
        Assert.Equal(first.ImageUrl, Assert.Single(_storage.Deleted));
        PlaceImage remaining = Assert.Single(_db.PlaceImages);
        Assert.Equal(second.Id, remaining.Id);
        Assert.Equal(0, remaining.SortOrder);
        Assert.Equal(3, _cache.InvalidateCount);
    }

    // Best effort: if storage cannot delete the file, the photo is still removed from the place.
    [Fact]
    public async Task DeleteAsync_StorageDeleteFails_StillRemovesPhoto()
    {
        AdminPlaceImageDto image = (await Upload(ImageSignatureTests.Jpeg()))!;
        _storage.FailDeletes = true;

        Assert.True(await _service.DeleteAsync(_place.Id, image.Id, AdminId));
        Assert.Empty(_db.PlaceImages);
    }

    [Fact]
    public async Task DeleteAsync_PhotoOfAnotherPlace_ReturnsFalse()
    {
        AdminPlaceImageDto image = (await Upload(ImageSignatureTests.Jpeg()))!;
        var other = new Place { Name = "Other", CategoryId = _place.CategoryId };
        _db.Places.Add(other);
        _db.SaveChanges();

        Assert.False(await _service.DeleteAsync(other.Id, image.Id, AdminId));
        Assert.Single(_db.PlaceImages);
    }

    [Fact]
    public async Task DeleteAsync_UnknownPhoto_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(_place.Id, 999, AdminId));
    }
}

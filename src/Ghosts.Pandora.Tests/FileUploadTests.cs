using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Ghosts.Pandora.Controllers;
using Microsoft.AspNetCore.Http;

namespace Ghosts.Pandora.Tests;

/// <summary>
/// Regression tests for GHSA-rx5q-vgh4-pcpm: uploads must not escape their directory, and
/// content displayed inline must be a real image rather than whatever the caller claims.
/// Uploads resolve against the current directory, so these run sequentially in one class.
/// </summary>
public class FileUploadTests : IDisposable
{
    // 1x1 opaque truecolor PNG: signature, IHDR, one deflated red pixel, IEND
    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR42mP4z8AAAAMBAQD3A0FDAAAAAElFTkSuQmCC";

    private readonly string _root;
    private readonly string _originalDirectory;

    public FileUploadTests()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _root = Path.Combine(Path.GetTempPath(), $"pandora-upload-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_root);
        Directory.SetCurrentDirectory(_root);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
            // a leftover temp directory is not worth failing a test run over
        }
    }

    private static FilesController.FileInputModel Upload(byte[] content, string fileName) =>
        new()
        {
            File = new FormFile(new MemoryStream(content), 0, content.Length, "File", fileName)
            {
                Headers = new HeaderDictionary()
            }
        };

    private string UploadsRoot => Path.Combine(_root, "uploads");
    private string ImagesRoot => Path.Combine(_root, "wwwroot", "images");

    [Fact]
    public async Task SaveFileAsync_StoresContentOutsideWwwrootUnderGeneratedNames()
    {
        var id = await Upload(Encoding.UTF8.GetBytes("payload"), "installer.msi").SaveFileAsync();

        var directory = Path.Combine(UploadsRoot, id.ToString());
        Assert.Equal("payload", await File.ReadAllTextAsync(Path.Combine(directory, "content")));
        Assert.Equal("installer.msi", await File.ReadAllTextAsync(Path.Combine(directory, "name")));

        // the caller's name is metadata only - it never becomes a stored filename
        Assert.Equal(["content", "name"], Directory.GetFiles(directory).Select(Path.GetFileName).Order());
        Assert.False(Directory.Exists(ImagesRoot));
    }

    [Fact]
    public async Task SaveFileAsync_TraversalFilename_StaysInsideUploads()
    {
        var id = await Upload(Encoding.UTF8.GetBytes("x"), "../../../../Views/Themes/default/Pwn.cshtml")
            .SaveFileAsync();

        Assert.True(File.Exists(Path.Combine(UploadsRoot, id.ToString(), "content")));
        Assert.Empty(Directory.GetFiles(_root, "*.cshtml", SearchOption.AllDirectories));

        // retained for Content-Disposition, reduced to its leaf
        Assert.Equal("Pwn.cshtml", await File.ReadAllTextAsync(Path.Combine(UploadsRoot, id.ToString(), "name")));
    }

    [Fact]
    public async Task SaveFileAsync_RejectsEmptyUpload()
    {
        var upload = Upload([], "empty.bin");
        await Assert.ThrowsAsync<InvalidOperationException>(() => upload.SaveFileAsync());
        Assert.False(Directory.Exists(UploadsRoot));
    }

    [Fact]
    public async Task SaveFileAsync_ConcurrentUploads_AreSerializedIntoDistinctDirectories()
    {
        var content = Encoding.UTF8.GetBytes("concurrent");

        var ids = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => Upload(content, $"file{i}.bin").SaveFileAsync()));

        Assert.Equal(8, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(File.Exists(Path.Combine(UploadsRoot, id.ToString(), "content"))));
    }

    /// <summary>
    /// The polyglot test below only proves anything if the fixture really is a decodable PNG, and a
    /// corrupt fixture is indistinguishable from a broken helper. This needs no native Skia, so it
    /// runs everywhere and fails by name.
    /// </summary>
    [Fact]
    public void TinyPng_FixtureIsAValidPngStream()
    {
        var png = Convert.FromBase64String(TinyPng);

        Assert.Equal(0x89, png[0]);
        Assert.Equal("PNG", Encoding.ASCII.GetString(png, 1, 3));
        Assert.Equal("IHDR", Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));

        // the previous fixture's pixel data failed its zlib checksum, which is what this catches
        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33, 4));
        Assert.Equal("IDAT", Encoding.ASCII.GetString(png, 37, 4));

        using var deflated = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress);
        using var pixels = new MemoryStream();
        deflated.CopyTo(pixels);

        // one filter byte plus one 8-bit RGB pixel
        Assert.Equal(4, pixels.Length);
    }

    [SkiaFact]
    public async Task SaveImageAsync_RejectsHtmlDisguisedAsPng()
    {
        var upload = Upload(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"), "innocent.png");

        await Assert.ThrowsAsync<InvalidOperationException>(() => upload.SaveImageAsync());
        Assert.False(Directory.Exists(ImagesRoot));
    }

    [SkiaFact]
    public async Task SaveImageAsync_RejectsSvg()
    {
        var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""";
        var upload = Upload(Encoding.UTF8.GetBytes(svg), "logo.svg");

        await Assert.ThrowsAsync<InvalidOperationException>(() => upload.SaveImageAsync());
        Assert.False(Directory.Exists(ImagesRoot));
    }

    [SkiaFact]
    public async Task SaveImageAsync_ReEncodesAndDropsAppendedContent()
    {
        var appended = Encoding.UTF8.GetBytes("<script>alert(1)</script>");
        var polyglot = Convert.FromBase64String(TinyPng).Concat(appended).ToArray();

        var served = await Upload(polyglot, "photo.png").SaveImageAsync();

        // the stored name is generated, never the supplied one
        Assert.Matches(@"^/images/[0-9a-f-]{36}/image\.(jpg|png)$", served);

        var stored = await File.ReadAllBytesAsync(Path.Combine(_root, "wwwroot", served.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        Assert.DoesNotContain(Encoding.UTF8.GetString(appended), Encoding.Latin1.GetString(stored));
    }

    [SkiaFact]
    public async Task SaveImageAsync_RejectsEmptyUpload()
    {
        var upload = Upload([], "empty.png");
        await Assert.ThrowsAsync<InvalidOperationException>(() => upload.SaveImageAsync());
    }
}

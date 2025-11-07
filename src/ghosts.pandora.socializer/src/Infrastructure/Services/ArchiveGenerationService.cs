using System.IO.Compression;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IArchiveGenerationService
{
    byte[] GenerateZip(int fileCount = 10);
    byte[] GenerateTar(int fileCount = 10);
}

public class ArchiveGenerationService : IArchiveGenerationService
{
    private readonly ILogger<ArchiveGenerationService> _logger;
    private readonly IDataFormatGenerationService _dataFormatService;
    private static readonly Random Random = new();

    public ArchiveGenerationService(
        ILogger<ArchiveGenerationService> logger,
        IDataFormatGenerationService dataFormatService)
    {
        _logger = logger;
        _dataFormatService = dataFormatService;
    }

    public byte[] GenerateZip(int fileCount = 10)
    {
        _logger.LogInformation("Generating ZIP archive with {FileCount} files", fileCount);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var files = CreateRandomFiles(fileCount);

            foreach (var (fileName, content) in files)
            {
                var entry = archive.CreateEntry(fileName);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
                _logger.LogDebug("Added {FileName} to ZIP archive", fileName);
            }
        }

        return memoryStream.ToArray();
    }

    public byte[] GenerateTar(int fileCount = 10)
    {
        _logger.LogInformation("Generating TAR archive with {FileCount} files", fileCount);

        using var memoryStream = new MemoryStream();
        using (var writer = WriterFactory.Open(memoryStream, ArchiveType.Tar, new WriterOptions(CompressionType.None)))
        {
            var files = CreateRandomFiles(fileCount);

            foreach (var (fileName, content) in files)
            {
                using var fileStream = new MemoryStream(content);
                writer.Write(fileName, fileStream);
                _logger.LogDebug("Added {FileName} to TAR archive", fileName);
            }
        }

        return memoryStream.ToArray();
    }

    private List<(string FileName, byte[] Content)> CreateRandomFiles(int count)
    {
        var files = new List<(string, byte[])>();

        for (var i = 0; i < count; i++)
        {
            var extension = Random.Next(3) switch
            {
                0 => ".txt",
                1 => ".json",
                _ => ".html"
            };

            var fileName = ContentGenerationHelper.GenerateRandomName(extension);
            byte[] content;

            switch (extension)
            {
                case ".txt":
                    content = System.Text.Encoding.UTF8.GetBytes(_dataFormatService.GenerateText(Random.Next(2, 5)));
                    break;
                case ".json":
                    content = System.Text.Encoding.UTF8.GetBytes(_dataFormatService.GenerateJson(Random.Next(3, 10)));
                    break;
                case ".html":
                    content = System.Text.Encoding.UTF8.GetBytes(_dataFormatService.GenerateHtml());
                    break;
                default:
                    content = System.Text.Encoding.UTF8.GetBytes(Faker.Lorem.Paragraph());
                    break;
            }

            files.Add((fileName, content));
            _logger.LogDebug("Created file {FileName} for archive", fileName);
        }

        return files;
    }
}

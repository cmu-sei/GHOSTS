using System.Text;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IVideoGenerationService
{
    byte[] GenerateVideo(string format = "mp4");
}

public class VideoGenerationService : IVideoGenerationService
{
    private readonly ILogger<VideoGenerationService> _logger;
    private static readonly Random Random = new();

    public VideoGenerationService(ILogger<VideoGenerationService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateVideo(string format = "mp4")
    {
        _logger.LogInformation("Generating {Format} video (fallback mode)", format);

        // Generate a minimal valid MP4 file structure
        // This is a simplified MP4 with ftyp and moov atoms
        // Real video generation would require FFmpeg or similar

        var video = new List<byte>();

        // ftyp atom (file type box) - identifies file as MP4
        var ftyp = new byte[]
        {
            0x00, 0x00, 0x00, 0x20, // atom size (32 bytes)
            0x66, 0x74, 0x79, 0x70, // 'ftyp'
            0x69, 0x73, 0x6F, 0x6D, // major brand 'isom'
            0x00, 0x00, 0x02, 0x00, // minor version
            0x69, 0x73, 0x6F, 0x6D, // compatible brand 'isom'
            0x69, 0x73, 0x6F, 0x32, // compatible brand 'iso2'
            0x61, 0x76, 0x63, 0x31, // compatible brand 'avc1'
            0x6D, 0x70, 0x34, 0x31  // compatible brand 'mp41'
        };

        video.AddRange(ftyp);

        // mdat atom (media data) - contains actual video data
        // For a fallback, we'll just add some random data
        var mdatSize = Random.Next(10000, 50000);
        var mdatHeader = new byte[]
        {
            (byte)((mdatSize >> 24) & 0xFF),
            (byte)((mdatSize >> 16) & 0xFF),
            (byte)((mdatSize >> 8) & 0xFF),
            (byte)(mdatSize & 0xFF),
            0x6D, 0x64, 0x61, 0x74  // 'mdat'
        };

        video.AddRange(mdatHeader);

        // Add random video data
        var videoData = new byte[mdatSize - 8];
        Random.NextBytes(videoData);
        video.AddRange(videoData);

        _logger.LogInformation("Generated fallback video of {Size} bytes", video.Count);

        return video.ToArray();
    }
}

using System.Text;

namespace Ghosts.Pandora.Infrastructure.Services;

public interface IAudioGenerationService
{
    byte[] GenerateAudio(string format = "wav");
}

public class AudioGenerationService : IAudioGenerationService
{
    private readonly ILogger<AudioGenerationService> _logger;
    private static readonly Random Random = new();

    public AudioGenerationService(ILogger<AudioGenerationService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateAudio(string format = "wav")
    {
        _logger.LogInformation("Generating {Format} audio file (fallback mode)", format);

        if (format.ToLower() == "mp3")
        {
            return GenerateMp3();
        }

        return GenerateWav();
    }

    private byte[] GenerateWav()
    {
        // Generate a minimal valid WAV file
        // WAV file structure: RIFF header, fmt chunk, data chunk

        var sampleRate = 44100;
        var bitsPerSample = 16;
        var numChannels = 2; // stereo
        var duration = Random.Next(5, 15); // 5-15 seconds
        var numSamples = sampleRate * duration * numChannels;
        var dataSize = numSamples * (bitsPerSample / 8);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize); // file size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // fmt chunk size
        writer.Write((short)1); // audio format (1 = PCM)
        writer.Write((short)numChannels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * numChannels * (bitsPerSample / 8)); // byte rate
        writer.Write((short)(numChannels * (bitsPerSample / 8))); // block align
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        // Generate simple audio data (sine wave for more realistic audio)
        var frequency = 440.0; // A note
        for (int i = 0; i < numSamples / numChannels; i++)
        {
            var t = i / (double)sampleRate;
            var sample = (short)(Math.Sin(2 * Math.PI * frequency * t) * 10000);

            // Add some variation
            if (Random.Next(100) < 10)
            {
                frequency = 440.0 + Random.Next(-100, 100);
            }

            // Write for both channels (stereo)
            writer.Write(sample);
            writer.Write(sample);
        }

        _logger.LogInformation("Generated WAV audio of {Size} bytes, duration {Duration}s", stream.Length, duration);

        return stream.ToArray();
    }

    private byte[] GenerateMp3()
    {
        // Generate a minimal MP3 file structure
        // This is a simplified MP3 with ID3v2 tag and frame headers
        // Real MP3 encoding would require a proper encoder

        var audio = new List<byte>();

        // ID3v2 header
        audio.AddRange(Encoding.ASCII.GetBytes("ID3"));
        audio.Add(0x03); // version
        audio.Add(0x00); // revision
        audio.Add(0x00); // flags
        // Size (synchsafe integer)
        audio.Add(0x00);
        audio.Add(0x00);
        audio.Add(0x00);
        audio.Add(0x00);

        // Add some MP3 frames (simplified)
        var frameCount = Random.Next(100, 500);
        for (int i = 0; i < frameCount; i++)
        {
            // MP3 frame header (11 bits of sync word)
            audio.Add(0xFF);
            audio.Add(0xFB);

            // Add random data for frame
            var frameSize = Random.Next(200, 400);
            var frameData = new byte[frameSize];
            Random.NextBytes(frameData);
            audio.AddRange(frameData);
        }

        _logger.LogInformation("Generated MP3 audio of {Size} bytes", audio.Count);

        return audio.ToArray();
    }
}

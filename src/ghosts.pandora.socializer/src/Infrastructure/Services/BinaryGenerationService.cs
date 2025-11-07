using System.Text;

namespace Ghosts.Socializer.Infrastructure.Services;

public interface IBinaryGenerationService
{
    byte[] GenerateBinary(int size = -1);
    byte[] GenerateOneNote();
    byte[] GenerateExecutable(string type = "exe");
    byte[] GenerateIso();
}

public class BinaryGenerationService : IBinaryGenerationService
{
    private readonly ILogger<BinaryGenerationService> _logger;
    private static readonly Random Random = new();

    public BinaryGenerationService(ILogger<BinaryGenerationService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateBinary(int size = -1)
    {
        if (size <= 0)
        {
            size = Random.Next(1000, 3000000);
        }

        _logger.LogInformation("Generating binary file of {Size} bytes", size);

        var data = new byte[size];
        Random.NextBytes(data);

        return data;
    }

    public byte[] GenerateOneNote()
    {
        _logger.LogInformation("Generating OneNote file");

        // OneNote files are complex, so we'll generate a fake binary blob
        var size = Random.Next(1000, 300000);
        var data = new byte[size];
        Random.NextBytes(data);

        // Add some OneNote-like header signatures (simplified)
        var header = new byte[]
        {
            0xE4, 0x52, 0x5C, 0x7B, 0x8C, 0xD8, 0xA7, 0x4D,
            0xAE, 0xB1, 0x53, 0x78, 0xD0, 0x29, 0x96, 0xD3
        };

        Buffer.BlockCopy(header, 0, data, 0, Math.Min(header.Length, data.Length));

        _logger.LogInformation("Generated OneNote file of {Size} bytes", data.Length);

        return data;
    }

    public byte[] GenerateExecutable(string type = "exe")
    {
        _logger.LogInformation("Generating {Type} executable (fake)", type);

        if (type.ToLower() == "msi")
        {
            return GenerateMsi();
        }

        return GenerateExe();
    }

    private byte[] GenerateExe()
    {
        // Generate a minimal PE (Portable Executable) header structure
        // This creates a recognizable but non-functional .exe file

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // MS-DOS header
        writer.Write((ushort)0x5A4D); // "MZ" signature
        writer.Write(new byte[58]); // MS-DOS stub

        // PE signature offset
        stream.Position = 0x3C;
        writer.Write(0x80); // PE header at offset 0x80

        // Pad to PE signature
        stream.Position = 0x80;

        // PE signature
        writer.Write(Encoding.ASCII.GetBytes("PE\0\0"));

        // COFF header
        writer.Write((ushort)0x014C); // Machine (i386)
        writer.Write((ushort)1); // Number of sections
        writer.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // Timestamp
        writer.Write((uint)0); // Symbol table pointer
        writer.Write((uint)0); // Number of symbols
        writer.Write((ushort)224); // Optional header size
        writer.Write((ushort)0x0102); // Characteristics

        // Optional header (simplified)
        writer.Write((ushort)0x010B); // Magic (PE32)
        writer.Write((byte)14); // Major linker version
        writer.Write((byte)0); // Minor linker version

        // Fill rest with random data
        var remainingSize = Random.Next(10000, 100000);
        var randomData = new byte[remainingSize];
        Random.NextBytes(randomData);
        writer.Write(randomData);

        _logger.LogInformation("Generated EXE file of {Size} bytes", stream.Length);

        return stream.ToArray();
    }

    private byte[] GenerateMsi()
    {
        // Generate a minimal MSI (Microsoft Installer) file structure
        // MSI files are OLE Compound Documents

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // OLE Compound Document header
        writer.Write(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }); // Signature

        // CLSID
        writer.Write(new byte[16]);

        // Minor version
        writer.Write((ushort)0x003E);

        // Major version
        writer.Write((ushort)0x0003);

        // Byte order
        writer.Write((ushort)0xFFFE);

        // Sector shift
        writer.Write((ushort)0x0009);

        // Mini sector shift
        writer.Write((ushort)0x0006);

        // Reserved
        writer.Write(new byte[6]);

        // Total sectors (0 for version 3)
        writer.Write((uint)0);

        // FAT sectors
        writer.Write((uint)1);

        // First directory sector
        writer.Write((uint)0);

        // Transaction signature
        writer.Write((uint)0);

        // Mini stream cutoff size
        writer.Write((uint)0x1000);

        // First mini FAT sector
        writer.Write(unchecked((uint)-2));

        // Number of mini FAT sectors
        writer.Write((uint)0);

        // First DIFAT sector
        writer.Write(unchecked((uint)-2));

        // Number of DIFAT sectors
        writer.Write((uint)0);

        // DIFAT array (109 entries)
        for (int i = 0; i < 109; i++)
        {
            writer.Write(unchecked((uint)-1));
        }

        // Add some random data to make it look more realistic
        var dataSize = Random.Next(50000, 200000);
        var randomData = new byte[dataSize];
        Random.NextBytes(randomData);
        writer.Write(randomData);

        _logger.LogInformation("Generated MSI file of {Size} bytes", stream.Length);

        return stream.ToArray();
    }

    public byte[] GenerateIso()
    {
        _logger.LogInformation("Generating ISO file");

        // Generate a minimal ISO 9660 file system structure
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // System area (first 32768 bytes are unused in ISO 9660)
        writer.Write(new byte[32768]);

        // Primary Volume Descriptor
        writer.Write((byte)1); // Type: Primary Volume Descriptor
        writer.Write(Encoding.ASCII.GetBytes("CD001")); // Identifier
        writer.Write((byte)1); // Version

        // Unused byte
        writer.Write((byte)0);

        // System identifier (32 bytes, padded with spaces)
        var systemId = "GHOSTS PANDORA".PadRight(32);
        writer.Write(Encoding.ASCII.GetBytes(systemId));

        // Volume identifier (32 bytes, padded with spaces)
        var volumeId = Faker.Lorem.GetFirstWord().ToUpper().PadRight(32);
        writer.Write(Encoding.ASCII.GetBytes(volumeId));

        // Unused (8 bytes)
        writer.Write(new byte[8]);

        // Volume space size (number of logical blocks)
        var blockCount = Random.Next(100, 1000);
        writer.Write(blockCount); // Little-endian
        writer.Write(ReverseBytes(blockCount)); // Big-endian

        // Unused (32 bytes)
        writer.Write(new byte[32]);

        // Volume set size
        writer.Write((ushort)1);
        writer.Write((ushort)0x0100);

        // Volume sequence number
        writer.Write((ushort)1);
        writer.Write((ushort)0x0100);

        // Logical block size (2048 bytes is standard for ISO 9660)
        writer.Write((ushort)2048);
        writer.Write((ushort)0x0008);

        // Fill rest of volume descriptor
        writer.Write(new byte[2048 - (int)stream.Position % 2048]);

        // Add some random blocks to make it more realistic
        var additionalBlocks = Random.Next(10, 50);
        for (int i = 0; i < additionalBlocks; i++)
        {
            var blockData = new byte[2048];
            Random.NextBytes(blockData);
            writer.Write(blockData);
        }

        _logger.LogInformation("Generated ISO file of {Size} bytes", stream.Length);

        return stream.ToArray();
    }

    private int ReverseBytes(int value)
    {
        return ((value & 0xFF) << 24) |
               ((value & 0xFF00) << 8) |
               ((value & 0xFF0000) >> 8) |
               ((value >> 24) & 0xFF);
    }
}

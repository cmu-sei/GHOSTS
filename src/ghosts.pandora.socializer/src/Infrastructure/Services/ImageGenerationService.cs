namespace Ghosts.Socializer.Infrastructure.Services;

public interface IImageGenerationService
{
    byte[] GenerateImage(string format = "png");
}

public class ImageGenerationService : IImageGenerationService
{
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(ILogger<ImageGenerationService> logger)
    {
        _logger = logger;
    }

    public byte[] GenerateImage(string format = "png")
    {
        _logger.LogInformation("Generating {Format} image", format);
        return ContentGenerationHelper.GenerateRandomImage(format);
    }
}

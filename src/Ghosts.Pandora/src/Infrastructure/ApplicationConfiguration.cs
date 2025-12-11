namespace Ghosts.Pandora.Infrastructure;

public class ApplicationConfiguration
{
    public int DefaultDisplay { get; set; }
    public int MinutesToCheckForDuplicatePost { get; set; }

    public CleanupJobConfig CleanupJob { get; set; }
    public CleanupAgeConfig CleanupAge { get; set; }

    public int CleanupDiskUtilThreshold { get; set; }

    public ModeConfig Mode { get; set; } = new();
    public PayloadsConfig Payloads { get; set; } = new();
    public PandoraConfig Pandora { get; set; } = new();

    public GhostsConfig Ghosts { get; set; }

    public class CleanupJobConfig
    {
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
    }

    public class CleanupAgeConfig
    {
        public int Days { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
    }

    public class ModeConfig
    {
        public string Type { get; set; } = "social"; // "social" or "website"

        // Social mode settings
        public string DefaultTheme { get; set; } = "facebook";

        // Website mode settings
        public string SiteType { get; set; } = "news"; // news, shopping, sports, entertainment
        public string SiteName { get; set; } = "Daily Chronicle";
        public int ArticleCount { get; set; } = 12;
    }

    public class PayloadsConfig
    {
        public bool Enabled { get; set; } = true;
        public string PayloadDirectory { get; set; } = "Payloads";
        public List<PayloadMapping> Mappings { get; set; } = new();
    }

    public class PayloadMapping
    {
        public string Url { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
    }

    public class PandoraConfig
    {
        public bool Enabled { get; set; } = true;
        public bool StoreResults { get; set; } = true;
        public string ContentCacheDirectory { get; set; } = "_data";
        public bool OllamaEnabled { get; set; } = false;
        public string OllamaApiUrl { get; set; } = "http://localhost:11434/api/generate";
        public int OllamaTimeout { get; set; } = 60;
        public OllamaModelsConfig OllamaModels { get; set; } = new();
        public ImageGenerationConfig ImageGeneration { get; set; } = new();
        public VideoGenerationConfig VideoGeneration { get; set; } = new();
        public VoiceGenerationConfig VoiceGeneration { get; set; } = new();
    }

    public class OllamaModelsConfig
    {
        public string Html { get; set; } = "llama3.2:3b";
        public string Image { get; set; } = "llama3.2";
        public string Json { get; set; } = "llama3.2";
        public string Ppt { get; set; } = "llama3.2";
        public string Script { get; set; } = "llama3.2";
        public string Stylesheet { get; set; } = "llama3.2";
        public string Text { get; set; } = "llama3.2";
        public string Voice { get; set; } = "llama3.2";
        public string Xlsx { get; set; } = "llama3.2";
        public string Pdf { get; set; } = "llama3.2";
        public string Csv { get; set; } = "llama3.2";
    }

    public class ImageGenerationConfig
    {
        public bool Enabled { get; set; } = false;
        public string Model { get; set; } = "stabilityai/sdxl-turbo";
    }

    public class VideoGenerationConfig
    {
        public bool Enabled { get; set; } = false;
    }

    public class VoiceGenerationConfig
    {
        public bool Enabled { get; set; } = false;
    }

    public class GhostsConfig
    {
        public string ApiUrl { get; set; }
        public string WorkflowsUrl { get; set; }
    }
}

public static class ApplicationConfigurationLoader
{
    public static ApplicationConfiguration Load()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json");

        var config = builder.Build();
        var appConfig = new ApplicationConfiguration();
        config.GetSection("ApplicationConfiguration").Bind(appConfig);

        return appConfig;
    }
}

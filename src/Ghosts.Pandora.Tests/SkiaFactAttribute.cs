namespace Ghosts.Pandora.Tests;

/// <summary>
/// A fact that needs a working native SkiaSharp. Decoding is a process-level crash rather
/// than an exception when the host's freetype does not match the bundled libSkiaSharp, so
/// these are opt-in via GHOSTS_SKIA_AVAILABLE=1 (set in CI) instead of failing the run.
/// </summary>
public sealed class SkiaFactAttribute : FactAttribute
{
    public SkiaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GHOSTS_SKIA_AVAILABLE") != "1")
            Skip = "Set GHOSTS_SKIA_AVAILABLE=1 to run image decoding tests.";
    }
}

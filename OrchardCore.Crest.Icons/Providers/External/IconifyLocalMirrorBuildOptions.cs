namespace Crest.Icons;

/// <summary>
/// Controls whether this build can use the bundled Iconify cache. Debug builds disable it by default so
/// local development exercises the public Iconify API and does not initialize the cache submodule.
/// </summary>
public static class IconifyLocalMirrorBuildOptions
{
#if CREST_ICONIFY_LOCAL_CACHE
    public static readonly bool Enabled = true;
#else
    public static readonly bool Enabled = false;
#endif
}

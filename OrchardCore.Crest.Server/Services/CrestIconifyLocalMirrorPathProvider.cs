using Crest.Iconify;
using Microsoft.AspNetCore.Hosting;

namespace Crest.Services;

public sealed class CrestIconifyLocalMirrorPathProvider(IWebHostEnvironment environment) : IIconifyLocalMirrorPathProvider
{
    public string RootPath => Path.Combine(environment.ContentRootPath, "App_Data", "OrchardCore.Crest", "Icons", "IconifyCache");

    public string SeedPath
    {
        get
        {
            var sourceCheckoutPath = Path.Combine(
                environment.ContentRootPath,
                "modules",
                "OrchardCore.Crest",
                "OrchardCore.Crest.Iconify",
                "icons",
                "Sources",
                "IconifyCache");

            if (File.Exists(Path.Combine(sourceCheckoutPath, "collections.json")))
            {
                return sourceCheckoutPath;
            }

            var legacySourceCheckoutPath = Path.Combine(
                environment.ContentRootPath,
                "modules",
                "OrchardCore.Crest",
                "OrchardCore.Crest.Icons",
                "icons",
                "Sources",
                "IconifyCache");

            if (File.Exists(Path.Combine(legacySourceCheckoutPath, "collections.json")))
            {
                return legacySourceCheckoutPath;
            }

            return Path.Combine(AppContext.BaseDirectory, "icons", "Sources", "IconifyCache");
        }
    }
}

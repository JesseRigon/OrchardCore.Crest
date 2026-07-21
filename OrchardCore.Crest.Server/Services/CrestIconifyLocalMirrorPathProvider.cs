using Crest.Icons;
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
                "OrchardCore.Crest.Icons",
                "icons",
                "Sources",
                "IconifyCache");

            return File.Exists(Path.Combine(sourceCheckoutPath, "collections.json"))
                ? sourceCheckoutPath
                : Path.Combine(AppContext.BaseDirectory, "icons", "Sources", "IconifyCache");
        }
    }
}

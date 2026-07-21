using BlazingOrchard.Icons;
using Microsoft.AspNetCore.Hosting;

namespace BlazingOrchard.Services;

public sealed class BlazingIconifyLocalMirrorPathProvider(IWebHostEnvironment environment) : IIconifyLocalMirrorPathProvider
{
    public string RootPath => Path.Combine(environment.ContentRootPath, "App_Data", "BlazingOrchard", "Icons", "IconifyCache");

    public string SeedPath
    {
        get
        {
            var sourceCheckoutPath = Path.Combine(
                environment.ContentRootPath,
                "modules",
                "BlazingOrchard.OrchardCoreModule",
                "BlazingOrchard.Icons",
                "icons",
                "Sources",
                "IconifyCache");

            return File.Exists(Path.Combine(sourceCheckoutPath, "collections.json"))
                ? sourceCheckoutPath
                : Path.Combine(AppContext.BaseDirectory, "icons", "Sources", "IconifyCache");
        }
    }
}

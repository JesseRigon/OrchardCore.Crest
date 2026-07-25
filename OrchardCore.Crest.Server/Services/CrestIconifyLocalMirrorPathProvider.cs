using Crest.Iconify;
using Microsoft.AspNetCore.Hosting;

namespace Crest.Services;

public sealed class CrestIconifyLocalMirrorPathProvider(IWebHostEnvironment environment) : IIconifyLocalMirrorPathProvider
{
    public string RootPath => Path.Combine(environment.ContentRootPath, "App_Data", "OrchardCore.Crest", "Icons", "IconifyCache");
}

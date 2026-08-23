using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Crest;
using Crest.Middlewares;

namespace Crest.ViewModels;

public sealed record CrestRoutingResponse(string AdminPath, string LoginPath);

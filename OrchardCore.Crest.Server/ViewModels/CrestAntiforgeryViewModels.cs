using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Crest.ViewModels;

public sealed record CrestAntiforgeryToken(string HeaderName, string RequestToken);

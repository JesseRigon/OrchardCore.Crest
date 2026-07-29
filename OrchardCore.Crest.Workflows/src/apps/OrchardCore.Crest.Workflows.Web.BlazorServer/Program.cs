using Elsa.Mediator.Options;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Crest.Workflows.Designer.Extensions;
using OrchardCore.Crest.Workflows.Options;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) =>
{
    logger
        .ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();
builder.Services.AddOrchardCms();
builder.Services.AddServerSideBlazor();
builder.Services.AddElsaDesigner(builder.Configuration);
builder.Services.Configure<MediatorOptions>(options => options.JobWorkerCount = 1);
builder.Services.Configure<ElsaStudioBlazorOptions>(options => options.RenderMode = RenderMode.Server);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseOrchardCore();

app.Run();

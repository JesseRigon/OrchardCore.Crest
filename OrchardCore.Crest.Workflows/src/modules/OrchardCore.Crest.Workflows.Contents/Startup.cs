using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Crest.Workflows.Contents.Contracts;
using OrchardCore.Crest.Workflows.Contents.Handlers;
using OrchardCore.Crest.Workflows.Contents.Services;
using OrchardCore.Crest.Workflows.Contents.UIHints;
using OrchardCore.Modules;

namespace OrchardCore.Crest.Workflows.Contents;

public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureElsa(elsa =>
        {
            elsa.AddActivitiesFrom<Startup>();
            elsa.AddVariableTypeAndAlias<ContentItem>("ContentItem", "CMS");
            elsa.UseJavaScript(jintOptions =>
            {
                jintOptions.ConfigureEngine((engine, ctx) => engine.SetValue("resolveTerm", (string taxonomyHandle, string termTitle) => ResolveTermAsync(ctx, taxonomyHandle, termTitle)));
            });
        });   
        services
            .AddScoped<IContentHandler, ContentEventHandler>()
            .AddScoped<IPropertyUIHandler, ContentTypeCheckListOptionsProvider>()
            .AddScoped<IPropertyUIHandler, ContentTypeOptionsProvider>()
            .AddScoped<ITaxonomyTermResolver, TaxonomyTermResolver>()
            ;
    }

    private async Task<object> ResolveTermAsync(ExpressionExecutionContext context, string taxonomyHandle, string termTitle)
    {
        var resolver = context.ServiceProvider.GetRequiredService<ITaxonomyTermResolver>();
        var terms = await resolver.ResolveTermsAsync(taxonomyHandle, [termTitle], context.CancellationToken);
        return terms.First();
    }
}
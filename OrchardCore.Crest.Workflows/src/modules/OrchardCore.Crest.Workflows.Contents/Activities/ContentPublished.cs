using Elsa.Workflows.Attributes;

namespace OrchardCore.Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Triggered when a content item draft has been published.")]
public class ContentPublished : ContentEventTriggerBase;
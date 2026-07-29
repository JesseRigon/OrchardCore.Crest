using Elsa.Workflows.Attributes;

namespace OrchardCore.Crest.Workflows.Contents.Activities;

[Activity("OrchardCore.Content", "Content", "Triggered when a content item is created.")]
public class ContentCreated : ContentEventTriggerBase;
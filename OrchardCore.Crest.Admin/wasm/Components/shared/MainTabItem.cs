namespace Crest.Admin.Components.Shared;

public sealed record MainTabItem<TValue>(TValue Value, string Text, string? Icon = null);

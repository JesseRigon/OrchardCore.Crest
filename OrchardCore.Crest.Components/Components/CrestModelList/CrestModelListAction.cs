using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives;

namespace Crest.Components.Model;

public sealed class CrestModelListAction<TItem>
{
    public string Icon { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ButtonStyle ButtonStyle { get; init; } = ButtonStyle.Light;
    public EventCallback<TItem> Callback { get; init; }
    public Func<TItem, Task>? Handler { get; init; }
    public Func<TItem, bool>? Disabled { get; init; }

    public Task InvokeAsync(TItem item) => Callback.HasDelegate
        ? Callback.InvokeAsync(item)
        : Handler?.Invoke(item) ?? Task.CompletedTask;
}

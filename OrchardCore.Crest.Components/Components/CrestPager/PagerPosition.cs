using System;

namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the position at which a Crest Blazor component renders its built-in <see cref="Crest.Components.Primitives.CrestPager" />.
/// </summary>
[Flags]
public enum PagerPosition
{
    /// <summary>
    /// CrestPager is displayed at the top of the component.
    /// </summary>
    Top = 1,

    /// <summary>
    /// CrestPager is displayed at the bottom of the component.
    /// </summary>
    Bottom = 2,

    /// <summary>
    /// CrestPager is displayed at the top and at the bottom of the component.
    /// </summary>
    TopAndBottom = Top | Bottom
}


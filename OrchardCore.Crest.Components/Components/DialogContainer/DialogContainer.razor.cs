using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Components.Primitives.Rendering
{
    /// <summary>
    /// DialogContainer component.
    /// </summary>
    [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2072, Justification = TrimMessages.ComponentTypePreserved)]
    [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2075, Justification = TrimMessages.ComponentTypePreserved)]
    public partial class DialogContainer
    {
        [Inject]
        private IServiceProvider Services { get; set; } = default!;

        private Localizer? localizer;
        internal Localizer Localizer => localizer ??= Services.GetService<Localizer>() ?? Localizer.Default;
        internal string CloseAriaLabel => Localizer.Get(nameof(CrestStrings.Dialog_CloseAriaLabel), CultureInfo.CurrentUICulture);
    }
}

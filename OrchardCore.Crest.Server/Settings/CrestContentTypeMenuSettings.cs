namespace Crest.Settings;

/// <summary>
/// Type-level designation: whether the content type gets its own entry under the
/// admin Content menu, linking to the generic content-items surface filtered to
/// it. Stored on the <c>ContentTypeDefinition</c>'s settings bag through the
/// standard <c>MergeSettings&lt;T&gt;</c>/<c>GetSettings&lt;T&gt;</c> pipeline, so
/// it travels with the definition (recipes, deployment) like any stock setting.
/// Module-declared pages are unaffected — this is the additive path for types no
/// module owns a page for.
/// </summary>
public class CrestContentTypeMenuSettings
{
    public bool ShowInContentMenu { get; set; }
}

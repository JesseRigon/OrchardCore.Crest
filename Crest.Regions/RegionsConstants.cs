namespace Crest.Regions;

public static class RegionsConstants
{
    public const string FeatureId = "Crest.Regions";

    public static class ContentTypes
    {
        /// <summary>A party-attached geo/localization profile. Never "Tenant" - see plans/regions-and-locations.md.</summary>
        public const string BusinessContext = "CrestBusinessContext";
    }

    public static class Parts
    {
        public const string BusinessContext = "CrestBusinessContextPart";

        /// <summary>
        /// Attachable by DOWNSTREAM modules to whatever carries a context (Parties attaches
        /// it to Person and Organization). Crest never names those types.
        /// </summary>
        public const string BusinessContextReference = "CrestBusinessContextReferencePart";
    }

    public static class Routes
    {
        public const string Api = "api/crest/regions";
    }
}

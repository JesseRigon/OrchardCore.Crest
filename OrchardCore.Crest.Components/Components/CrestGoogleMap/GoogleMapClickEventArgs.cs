namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestGoogleMap.MapClick" /> event that is being raised.
/// </summary>
public class GoogleMapClickEventArgs
{
    /// <summary>
    /// The position which represents the clicked map location.
    /// </summary>
    public GoogleMapPosition? Position { get; set; }
}


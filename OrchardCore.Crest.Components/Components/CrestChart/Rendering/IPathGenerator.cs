using System.Collections.Generic;

namespace Crest.Components.Primitives.Rendering
{
    /// <summary>
    /// Interface IPathGenerator
    /// </summary>
    public interface IPathGenerator
    {
        /// <summary>
        /// Pathes the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>System.String.</returns>
        string Path(IEnumerable<Point> data);
    }
}

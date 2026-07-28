using System;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Displays the median of a chart series.
    /// </summary>
    /// <example>
    /// <code>
    ///   &lt;CrestChart&gt;
    ///       &lt;CrestLineSeries Data=@revenue CategoryProperty="Quarter" ValueProperty="Revenue"&gt;
    ///          &lt;CrestSeriesMedianLine /&gt;
    ///       &lt;/CrestLineSeries&gt;
    ///   &lt;/CrestChart&gt;
    ///   @code {
    ///       class DataItem
    ///       {
    ///           public string Quarter { get; set; }
    ///           public double Revenue { get; set; }
    ///       }
    ///       DataItem[] revenue = new DataItem[]
    ///       {
    ///           new DataItem { Quarter = "Q1", Revenue = 234000 },
    ///           new DataItem { Quarter = "Q2", Revenue = 284000 },
    ///           new DataItem { Quarter = "Q3", Revenue = 274000 },
    ///           new DataItem { Quarter = "Q4", Revenue = 294000 }
    ///       };
    ///   }
    /// </code>
    /// </example>
    public partial class CrestSeriesMedianLine : CrestSeriesValueLine
    {
        /// <ihnheritdoc />
        public override double Value
        {
            get
            {
                return Series?.GetMedian() ?? 0;
            }
            set
            {
                throw new InvalidOperationException("Value is computed and cannot be set");
            }
        }

        /// <ihnheritdoc />
        protected override string Name => "Median";
    }
}

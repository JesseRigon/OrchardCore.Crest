using System;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Supplies information about a <see cref="CrestScheduler{TItem}.TodaySelect" /> event that is being raised.
    /// </summary>
    public class SchedulerTodaySelectEventArgs
    {
        /// <summary>
        /// Today's date. You can change this value to navigate to a different date.
        /// </summary>
        public DateTime Today { get; set; }
    }
}
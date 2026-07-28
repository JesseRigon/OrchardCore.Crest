using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;
using System;
using System.Globalization;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Displays the appointments in a month day in <see cref="CrestScheduler{TItem}" />
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestScheduler Data="@appointments"&gt;
    ///     &lt;CrestMonthView /&gt;
    /// &lt;/CrestScheduler&gt;
    /// </code>
    /// </example>
    public partial class CrestYearView : SchedulerYearViewBase
    {
        /// <inheritdoc />
        public override string Icon => "calendar_month";

        /// <inheritdoc />
        [Parameter]
        public override string Text { get => text ?? Localize(nameof(CrestStrings.YearView_Text)); set => text = value; }
        private string? text;

        private string? moreText;

        /// <summary>
        /// Specifies the text displayed when there are more appointments in a slot than MaxAppointmentsInSlot.
        /// </summary>
        /// <value>The more text. Set to <c>"+ {0} more"</c> by default.</value>
        [Parameter]
        public string MoreText { get => moreText ?? Localize(nameof(CrestStrings.YearView_MoreText)); set => moreText = value; }

        private string? noDayEventsText;

        /// <summary>
        /// Specifies the text displayed when the user clicks on a day with no events in the year view
        /// </summary>
        [Parameter]
        public string NoDayEventsText { get => noDayEventsText ?? Localize(nameof(CrestStrings.YearView_NoDayEventsText)); set => noDayEventsText = value; }

        /// <inheritdoc />
        public override DateTime StartDate
        {
            get
            {
                return Scheduler == null ? DateTime.Today : GetYearRange().viewStart;
            }
        }

        /// <inheritdoc />
        public override DateTime EndDate
        {
            get
            {
                return Scheduler == null ? DateTime.Today : GetYearRange().viewEnd;
            }
        }

        /// <summary>
        /// Gets or sets the start month for the year views />.
        /// </summary>
        /// <value>The start month.</value>
        [Parameter]
        public override Month StartMonth { get; set; } = Month.January;

        /// <inheritdoc />
        public override DateTime Next()
        {
            return Scheduler?.CurrentDate.Date.AddYears(1) ?? DateTime.Today.AddYears(1);
        }

        /// <inheritdoc />
        public override DateTime Prev()
        {
            return Scheduler?.CurrentDate.Date.AddYears(-1) ?? DateTime.Today.AddYears(-1);
        }
    }
}

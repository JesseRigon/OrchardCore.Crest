using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;
using System;
using System.Drawing;
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
    public partial class CrestYearTimelineView : SchedulerYearViewBase
    {
        /// <inheritdoc />
        public override string Icon => "view_timeline";

        /// <inheritdoc />
        [Parameter]
        public override string Text { get => text ?? Localize(nameof(CrestStrings.YearTimelineView_Text)); set => text = value; }
        private string? text;

        /// <summary>
        /// Specifies the maximum appointnments to render in a slot.
        /// </summary>
        /// <value>The maximum appointments in slot.</value>
        [Parameter]
        public int? MaxAppointmentsInSlot { get; set; }

        private string? moreText;

        /// <summary>
        /// Specifies the text displayed when there are more appointments in a slot than <see cref="MaxAppointmentsInSlot" />.
        /// </summary>
        /// <value>The more text. Set to <c>"+ {0} more"</c> by default.</value>
        [Parameter]
        public string MoreText { get => moreText ?? Localize(nameof(CrestStrings.YearTimelineView_MoreText)); set => moreText = value; }

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

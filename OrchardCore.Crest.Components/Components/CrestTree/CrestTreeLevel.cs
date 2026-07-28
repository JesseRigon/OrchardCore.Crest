using Microsoft.AspNetCore.Components;
using System;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Configures a level of nodes in a <see cref="CrestTree" /> during data-binding.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestTree Data=@rootEmployees&gt;
    ///     &lt;CrestTreeLevel TextProperty="LastName" ChildrenProperty="Employees1"  HasChildren=@(e =&gt; (e as Employee).Employees1.Any()) /&gt;
    /// &lt;/CrestTree&gt;
    /// @code {
    ///  IEnumerable&lt;Employee&gt; rootEmployees; 
    ///  protected override void OnInitialized()
    ///  {
    ///     rootEmployees = NorthwindDbContext.Employees.Where(e => e.ReportsTo == null);
    ///  }
    /// }
    /// </code>
    /// </example>
    public class CrestTreeLevel : ComponentBase
    {
        /// <summary>
        /// Specifies the name of the property which provides values for the <see cref="CrestTreeItem.Text" /> property of the child items.
        /// </summary>
        [Parameter]
        public string? TextProperty { get; set; }

        /// <summary>
        /// Specifies the name of the property which provides values for the <see cref="CrestTreeItem.Checkable" /> property of the child items.
        /// </summary>
        [Parameter]
        public string? CheckableProperty { get; set; }

        /// <summary>
        /// Specifies the name of the property which returns child data. The value returned by that property should be IEnumerable
        /// </summary>
        [Parameter]
        public string? ChildrenProperty { get; set; }

        /// <summary>
        /// Determines if a child item has children or not. Set to <c>value =&gt; true</c> by default.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;CrestTreeLevel HasChildren=@(e =&gt; (e as Employee).Employees1.Any()) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? HasChildren { get; set; } = value => true;

        /// <summary>
        /// Determines if a child item is expanded or not. Set to <c>value =&gt; false</c> by default.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;CrestTreeLevel Expanded=@(e =&gt; (e as Employee).Employees1.Any()) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? Expanded { get; set; } = value => false;

        /// <summary>
        /// Determines if a child item is selected or not. Set to <c>value =&gt; false</c> by default.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;CrestTreeLevel Selected=@(e =&gt; (e as Employee).LastName == "Fuller") /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? Selected { get; set; } = value => false;

        /// <summary>
        /// Determines the text of a child item.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;CrestTreeLevel Text=@(e =&gt; (e as Employee).LastName) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, string>? Text { get; set; }

        /// <summary>
        /// Determines the if the checkbox of the child item can be checked.
        /// </summary>
        /// <example>
        /// <code>
        ///     &lt;CrestTreeLevel Checkable=@(e =&gt; (e as Employee).LastName != null) /&gt;
        /// </code>
        /// </example>
        [Parameter]
        public Func<object, bool>? Checkable { get; set; }

        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        [Parameter]
        public RenderFragment<CrestTreeItem>? Template { get; set; }

        private CrestTree? _tree;

        /// <summary>
        /// The CrestTree which this item is part of.
        /// </summary>
        [CascadingParameter]
        public CrestTree? Tree
        {
            get => _tree;
            set
            {
                if (value != null)
                {
                    value.AddLevel(this);
                }

                _tree = value;
            }
        }
    }
}
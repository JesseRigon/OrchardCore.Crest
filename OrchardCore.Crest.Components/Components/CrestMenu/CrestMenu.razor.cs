using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Crest.Components.Primitives.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A horizontal menu component with support for nested submenus, icons, and responsive behavior.
    /// CrestMenu provides a classic menu bar for navigation, typically used in application headers or toolbars.
    /// Displays menu items horizontally with dropdown submenus.
    /// Supports multi-level nested menus via CrestMenuItem child items, automatic navigation via Path property or custom Click handlers,
    /// icons displayed alongside menu item text, responsive design that automatically collapses to a hamburger menu on small screens (configurable),
    /// click-to-open or hover-to-open interaction modes, keyboard navigation (Arrow keys, Enter, Escape) for accessibility, and visual separators between menu items.
    /// Use for application navigation bars, command menus, or toolbar-style interfaces. Menu items are defined using CrestMenuItem components as child content.
    /// </summary>
    /// <example>
    /// Basic menu with navigation:
    /// <code>
    /// &lt;CrestMenu&gt;
    ///     &lt;CrestMenuItem Text="Home" Path="/" Icon="home" /&gt;
    ///     &lt;CrestMenuItem Text="Data"&gt;
    ///         &lt;CrestMenuItem Text="Orders" Path="/orders" /&gt;
    ///         &lt;CrestMenuItem Text="Customers" Path="/customers" /&gt;
    ///     &lt;/CrestMenuItem&gt;
    ///     &lt;CrestMenuItem Text="Reports" Path="/reports" /&gt;
    /// &lt;/CrestMenu&gt;
    /// </code>
    /// Menu with click handlers:
    /// <code>
    /// &lt;CrestMenu Click=@OnMenuClick&gt;
    ///     &lt;CrestMenuItem Text="File"&gt;
    ///         &lt;CrestMenuItem Text="New" Value="new" Icon="add" /&gt;
    ///         &lt;CrestMenuItem Text="Open" Value="open" Icon="folder_open" /&gt;
    ///         &lt;CrestMenuItem Text="Save" Value="save" Icon="save" /&gt;
    ///     &lt;/CrestMenuItem&gt;
    /// &lt;/CrestMenu&gt;
    /// </code>
    /// </example>
    public partial class CrestMenu : CrestComponentWithChildren
    {
        /// <summary>
        /// Gets or sets whether the menu should automatically collapse to a hamburger menu on small screens.
        /// When enabled, displays a toggle button that expands/collapses the menu on mobile devices.
        /// </summary>
        /// <value><c>true</c> to enable responsive behavior with hamburger menu; <c>false</c> for always-horizontal menu. Default is <c>true</c>.</value>
        [Parameter]
        public bool Responsive { get; set; } = true;

        /// <summary>
        /// Gets or sets the interaction mode for opening submenus.
        /// When true, submenus open on click. When false, submenus open on hover (desktop) and click (touch devices).
        /// </summary>
        /// <value><c>true</c> to open on click; <c>false</c> to open on hover. Default is <c>true</c>.</value>
        [Parameter]
        public bool ClickToOpen { get; set; } = true;

        /// <summary>
        /// Gets or sets whether nested submenus should fly out horizontally to the side instead of expanding vertically inline.
        /// When enabled, 2nd level and deeper submenus appear as cascading flyout menus positioned to the right of their parent item.
        /// </summary>
        /// <value><c>true</c> to enable flyout submenus; <c>false</c> for default accordion-style nesting. Default is <c>false</c>.</value>
        [Parameter]
        public bool Flyout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this menu is rendered as a context menu popup.
        /// When enabled, the root element uses role="menu" with vertical orientation instead of a horizontal menubar.
        /// </summary>
        /// <value><c>true</c> to render as a vertical context menu popup; otherwise <c>false</c>. Default is <c>false</c>.</value>
        [Parameter]
        public bool IsContextMenu { get; set; }

        private bool IsOpen { get; set; }

        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-menu")
                                                                     .Add("rz-menu-open", Responsive && IsOpen)
                                                                     .Add("rz-menu-closed", Responsive && !IsOpen)
                                                                     .Add("rz-menu-flyout", Flyout)
                                                                     .ToString();

        IJSObjectReference? _jsRef;
        bool _clickToOpenChanged;

        /// <inheritdoc />
        public override async Task SetParametersAsync(ParameterView parameters)
        {
            if (parameters.DidParameterChange(nameof(ClickToOpen), ClickToOpen) ||
                parameters.DidParameterChange(nameof(Visible), Visible))
            {
                _clickToOpenChanged = true;
            }

            await base.SetParametersAsync(parameters);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if ((firstRender || _clickToOpenChanged) && JSRuntime != null)
            {
                _clickToOpenChanged = false;

                if (_jsRef != null)
                {
                    await _jsRef.InvokeVoidAsync("dispose");
                    await _jsRef.DisposeAsync();
                    _jsRef = null;
                }

                if (Visible)
                {
                    _jsRef = await JSRuntime.InvokeAsync<IJSObjectReference>(
                        "Crest.createMenu", Element, ClickToOpen);
                }
            }
        }

        void OnToggle()
        {
            IsOpen = !IsOpen;
        }

        /// <summary>
        /// Gets or sets the click callback.
        /// </summary>
        /// <value>The click callback.</value>
        [Parameter]
        public EventCallback<MenuItemEventArgs> Click { get; set; }

        /// <summary>
        /// Gets or sets a callback invoked when the menu requests to be dismissed, such as pressing Escape at the root of a context menu.
        /// </summary>
        /// <value>The close callback.</value>
        [Parameter]
        public EventCallback Close { get; set; }

        private string? ariaLabel;

        /// <summary>
        /// Gets or sets the menu aria label text.
        /// </summary>
        /// <value>The menu aria label text.</value>
        [Parameter]
        public string AriaLabel { get => ariaLabel ?? Localize(nameof(CrestStrings.Menu_AriaLabel)); set => ariaLabel = value; }

        [Inject]
        NavigationManager? NavigationManager { get; set; }

        bool subMenuOpen;
        internal int focusedIndex = -1;
        bool preventKeyPress = true;
        bool stopKeydownPropagation;
        async Task OnKeyPress(KeyboardEventArgs args)
        {
            var key = args.Code != null ? args.Code : args.Key;

            if (currentItems.Count == 0)
            {
                currentItems = items.Where(i => i.Visible && !i.Disabled).ToList();
            }

            if (key == "Home" || key == "End")
            {
                preventKeyPress = true;
                stopKeydownPropagation = true;

                if (currentItems.Count > 0)
                {
                    focusedIndex = key == "Home" ? 0 : currentItems.Count - 1;
                }
            }
            else if (key == "ArrowUp" || key == "ArrowDown")
            {
                preventKeyPress = true;
                stopKeydownPropagation = true;

                if (subMenuOpen || IsContextMenu)
                {
                    if (currentItems.Count > 0)
                    {
                        var start = Math.Clamp(focusedIndex, 0, currentItems.Count - 1);
                        focusedIndex = (start + (key == "ArrowUp" ? -1 : 1) + currentItems.Count) % currentItems.Count;
                    }
                }
                else
                {
                    if (currentItems.Count > 0)
                    {
                        focusedIndex = Math.Clamp(focusedIndex, 0, currentItems.Count - 1);

                        var item = currentItems[focusedIndex];

                        if (item.items.Count > 0)
                        {
                            currentItems = item.items.Where(i => i.Visible && !i.Disabled).ToList();
                            focusedIndex = key == "ArrowUp" ? currentItems.Count - 1 : 0;
                            subMenuOpen = true;
                            await item.Open();
                        }
                    }
                }
            }
            else if (key == "ArrowLeft" || key == "ArrowRight")
            {
                preventKeyPress = true;
                stopKeydownPropagation = true;

                if (IsContextMenu)
                {
                    if (key == "ArrowRight" && focusedIndex >= 0 && focusedIndex < currentItems.Count)
                    {
                        var item = currentItems[focusedIndex];
                        if (item.items.Count > 0)
                        {
                            currentItems = item.items.Where(i => i.Visible && !i.Disabled).ToList();
                            focusedIndex = 0;
                            subMenuOpen = true;
                            await item.Open();
                        }
                    }
                    else if (key == "ArrowLeft" && subMenuOpen)
                    {
                        var firstItem = currentItems.FirstOrDefault();
                        var parentItem = firstItem?.ParentItem;
                        if (parentItem != null)
                        {
                            currentItems = (parentItem.ParentItem != null ? parentItem.ParentItem.items : parentItem.Parent?.items ?? new List<CrestMenuItem>()).Where(i => i.Visible && !i.Disabled).ToList();
                            focusedIndex = currentItems.IndexOf(parentItem);
                            subMenuOpen = parentItem.ParentItem != null;
                            await parentItem.Close();
                        }
                    }

                    return;
                }

                if (subMenuOpen)
                {
                    if (key == "ArrowRight" && focusedIndex >= 0 && focusedIndex < currentItems.Count)
                    {
                        var item = currentItems[focusedIndex];
                        if (item.items.Count > 0)
                        {
                            currentItems = item.items.Where(i => i.Visible && !i.Disabled).ToList();
                            focusedIndex = 0;
                            subMenuOpen = true;
                            await item.Open();
                            return;
                        }
                    }
                    else if (key == "ArrowLeft")
                    {
                        var firstItem = currentItems.FirstOrDefault();
                        var parentItem = firstItem?.ParentItem;
                        if (parentItem?.ParentItem != null)
                        {
                            currentItems = parentItem.ParentItem.items.Where(i => i.Visible && !i.Disabled).ToList();
                            focusedIndex = currentItems.IndexOf(parentItem);
                            subMenuOpen = true;
                            await parentItem.Close();
                            return;
                        }
                    }
                }

                bool shouldOpenNextMenu = false;
                if (subMenuOpen)
                {
                    var firstItem = currentItems.FirstOrDefault();
                    var parentItem = firstItem?.ParentItem;
                    if (parentItem != null && parentItem.Parent != null)
                    {
                        currentItems = parentItem.Parent.items.Where(i => i.Visible && !i.Disabled).ToList();
                        focusedIndex = currentItems.IndexOf(parentItem);
                        subMenuOpen = false;
                        await parentItem.Close();
                        shouldOpenNextMenu = true;
                    }
                }

                focusedIndex = Math.Clamp(focusedIndex + (key == "ArrowLeft" ? -1 : 1), 0, currentItems.Count - 1);

                if (shouldOpenNextMenu)
                {
                    shouldOpenNextMenu = false;

                    var item = currentItems[focusedIndex];

                    if (item.items.Count > 0)
                    {
                        currentItems = item.items.Where(i => i.Visible && !i.Disabled).ToList();
                        focusedIndex = 0;
                        subMenuOpen = true;
                        await item.Toggle();
                    }
                }
            }
            else if (key == "Space" || key == "Enter")
            {
                preventKeyPress = true;
                stopKeydownPropagation = true;

                if (focusedIndex >= 0 && focusedIndex < currentItems.Count)
                {
                    var item = currentItems[focusedIndex];

                    if (item.items.Count > 0)
                    {
                        currentItems = item.items.Where(i => i.Visible && !i.Disabled).ToList();
                        focusedIndex = 0;
                        subMenuOpen = true;
                        await item.Toggle();
                    }
                    else
                    {
                        if (item.Path != null)
                        {
                            NavigationManager?.NavigateTo(item.Path);
                        }
                        else
                        {
                            await item.OnClick(new MouseEventArgs());
                        }
                    }
                }
            }
            else if (key == "Escape")
            {
                preventKeyPress = true;
                stopKeydownPropagation = true;

                if (currentItems.Any(i => i.ParentItem != null))
                {
                    var firstItem = currentItems.FirstOrDefault();
                    var parentItem = firstItem?.ParentItem;
                    if (parentItem != null)
                    {
                        currentItems = (parentItem.ParentItem != null ? parentItem.ParentItem.items : parentItem.Parent?.items ?? new List<CrestMenuItem>()).Where(i => i.Visible && !i.Disabled).ToList();
                        focusedIndex = currentItems.IndexOf(parentItem);
                        subMenuOpen = parentItem.ParentItem != null;
                        await parentItem.Close();
                    }
                }
                else if (IsContextMenu && Close.HasDelegate)
                {
                    await Close.InvokeAsync();
                }
            }
            else if (args.Key != null && args.Key.Length == 1 && !char.IsControl(args.Key[0]) && currentItems.Count > 0)
            {
                preventKeyPress = true;
                stopKeydownPropagation = true;

                var search = args.Key;

                for (var offset = 1; offset <= currentItems.Count; offset++)
                {
                    var index = (focusedIndex + offset) % currentItems.Count;
                    var text = currentItems[index].Text;

                    if (text != null && text.StartsWith(search, StringComparison.OrdinalIgnoreCase))
                    {
                        focusedIndex = index;
                        break;
                    }
                }
            }
            else
            {
                preventKeyPress = false;
                stopKeydownPropagation = false;
            }
        }

        internal bool IsFocused(CrestMenuItem item)
        {
            return focusedIndex != -1 && currentItems.IndexOf(item) == focusedIndex;
        }

        internal CrestMenuItem? ActiveItem => focusedIndex >= 0 && focusedIndex < currentItems.Count ? currentItems[focusedIndex] : null;

        string? ActiveDescendantId => ActiveItem?.GetMenuItemId();

        List<CrestMenuItem> currentItems = new();

        internal List<CrestMenuItem> items = new List<CrestMenuItem>();

        /// <summary>
        /// Adds the item.
        /// </summary>
        /// <param name="item">The item.</param>
        public void AddItem(CrestMenuItem item)
        {
            if (items.IndexOf(item) == -1)
            {
                items.Add(item);
                StateHasChanged();
            }
        }

        private string? toggleAriaLabel;

        /// <summary>
        /// Gets or sets the add button aria-label attribute.
        /// </summary>
        [Parameter]
        public string ToggleAriaLabel { get => toggleAriaLabel ?? Localize(nameof(CrestStrings.Menu_ToggleAriaLabel)); set => toggleAriaLabel = value; }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            if (NavigationManager != null)
            {
                NavigationManager.LocationChanged += OnLocationChanged;
            }
        }

        private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
        {
            IsOpen = false;
            StateHasChanged();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            base.Dispose();
            if (NavigationManager != null)
            {
                NavigationManager.LocationChanged -= OnLocationChanged;
            }
            _jsRef?.InvokeVoidAsync("dispose");
            _jsRef?.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        void OnFocus()
        {
            if (currentItems.Count == 0)
            {
                currentItems = items.Where(i => i.Visible && !i.Disabled).ToList();
            }

            focusedIndex = focusedIndex == -1 ? 0 : focusedIndex;
        }
    }
}
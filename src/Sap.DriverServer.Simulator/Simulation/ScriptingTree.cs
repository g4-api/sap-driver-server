using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Maps WPF elements to SAP GUI Scripting identifiers and reads their screen geometry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Screens mark each scriptable element with the <c>Id</c> attached property, holding one SAP identifier segment
    /// such as <c>txtRSYST-BNAME</c> or <c>btn[3]</c>. The segment's prefix selects the SAP type, as in SAP GUI.
    /// An element's scripting children are its nearest marked descendants in the logical tree; unmarked panels in
    /// between are layout only. Collapsed elements and the content of unselected tabs are not part of the tree,
    /// because SAP GUI exposes only what the current screen shows.
    /// </para>
    /// <para>
    /// Geometry is reported in physical screen pixels. The process is per-monitor DPI aware, so
    /// <see cref="Visual.PointToScreen(Point)"/> already returns physical pixels.
    /// </para>
    /// </remarks>
    public static class ScriptingTree
    {
        #region *** Fields       ***
        /// <summary>
        /// Identifies the attached property that holds an element's SAP identifier segment.
        /// </summary>
        public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
            "Id",
            typeof(string),
            typeof(ScriptingTree),
            new PropertyMetadata(defaultValue: null));
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Finds the nearest marked element at or above a starting point, such as the control that owns a focused part.
        /// </summary>
        /// <param name="start">The starting element, such as the keyboard focus.</param>
        /// <returns>The marked element; null when none exists below the window.</returns>
        public static FrameworkElement FindElement(DependencyObject start)
        {
            // Walk up through template parts and layout elements until a marked element or the window is reached.
            for (var current = start; current is not null and not Window; current = GetParent(current))
            {
                if (current is FrameworkElement element && GetId(element) is not null)
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the scripting children of an element: its nearest marked, visible descendants in the logical tree.
        /// </summary>
        /// <param name="parent">The element whose children are read.</param>
        /// <returns>The children in document order; empty when the element has none.</returns>
        public static IReadOnlyList<FrameworkElement> GetChildElements(DependencyObject parent)
        {
            // Collect the marked descendants, descending only through unmarked layout elements.
            var children = new List<FrameworkElement>();
            AddChildElements(parent, children);

            return children;
        }

        /// <summary>
        /// Gets the identifier segment of an element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The segment, such as <c>btn[0]</c>; null when the element is not scriptable.</returns>
        public static string GetId(DependencyObject element)
        {
            return (string)element.GetValue(IdProperty);
        }

        /// <summary>
        /// Gets the nearest marked ancestor of an element inside its window.
        /// </summary>
        /// <param name="element">The scriptable element.</param>
        /// <returns>The ancestor; null when the element's parent is the window itself.</returns>
        public static FrameworkElement GetParentElement(FrameworkElement element)
        {
            // Start above the element, so the element itself is never its own parent.
            return FindElement(GetParent(element));
        }

        /// <summary>
        /// Gets the identifier of an element relative to its frame window, such as <c>usr/txtRSYST-BNAME</c>.
        /// </summary>
        /// <param name="element">The scriptable element.</param>
        /// <returns>The relative identifier; the element's own segment when no marked ancestor exists.</returns>
        public static string GetRelativeId(FrameworkElement element)
        {
            // Collect the segments of the element and its marked ancestors up to the window.
            var segments = new List<string>();

            for (var current = (DependencyObject)element; current is not null and not Window; current = GetParent(current))
            {
                var segment = GetId(current);
                if (segment is not null)
                {
                    segments.Add(segment);
                }
            }

            segments.Reverse();

            return string.Join('/', segments);
        }

        /// <summary>
        /// Gets the screen rectangle of an element in physical pixels.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <returns>The rectangle; <see cref="Rect.Empty"/> when the element is not displayed.</returns>
        public static Rect GetScreenBounds(FrameworkElement element)
        {
            // Report nothing for elements that are hidden, unloaded, or in a closed window.
            if (!element.IsVisible || PresentationSource.FromVisual(element) is null)
            {
                return Rect.Empty;
            }

            // Bring the layout up to date, so geometry read right after a change is already correct.
            element.UpdateLayout();
            var topLeft = element.PointToScreen(new Point(0, 0));
            var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));

            return new Rect(topLeft, bottomRight);
        }

        /// <summary>
        /// Sets the identifier segment of an element.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="value">The segment, such as <c>btn[0]</c>.</param>
        public static void SetId(DependencyObject element, string value)
        {
            element.SetValue(IdProperty, value);
        }

        // Adds the nearest marked descendants of a parent, skipping hidden elements and unselected tab content.
        private static void AddChildElements(DependencyObject parent, List<FrameworkElement> children)
        {
            // An unselected tab shows only its header; SAP GUI does not expose the content behind it.
            if (parent is TabItem { IsSelected: false })
            {
                return;
            }

            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<FrameworkElement>())
            {
                // Skip what the screen does not show; descend through unmarked layout elements.
                if (child.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (GetId(child) is not null)
                {
                    children.Add(child);
                }
                else
                {
                    AddChildElements(child, children);
                }
            }
        }

        // Gets the logical parent, falling back to the visual parent for elements created by templates.
        private static DependencyObject GetParent(DependencyObject element)
        {
            // Prefer the logical tree, which is how screens nest their marked elements.
            return LogicalTreeHelper.GetParent(element) ?? (element is Visual ? VisualTreeHelper.GetParent(element) : null);
        }
        #endregion
    }
}

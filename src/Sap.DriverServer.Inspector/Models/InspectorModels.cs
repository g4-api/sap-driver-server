using System.Collections.Generic;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Models
{
    /// <summary>
    /// Represents one scripting object in the inspector tree, read without its children.
    /// </summary>
    /// <remarks>
    /// Summaries are COM-free snapshots taken inside one scripting operation. Password text is never read into them.
    /// </remarks>
    public class ElementSummary
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the absolute SAP GUI Scripting identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the object exposes a <c>Children</c> collection worth expanding.
        /// </summary>
        public bool IsContainer { get; set; }

        /// <summary>
        /// Gets or sets the SAP <c>Name</c> property.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the display text; empty for password fields and objects without text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the SAP <c>Type</c>, such as <c>GuiButton</c>.
        /// </summary>
        public string Type { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents one property row in the details pane.
    /// </summary>
    public class PropertyEntry
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the property belongs to the basic set shown by default.
        /// </summary>
        public bool IsBasic { get; set; }

        /// <summary>
        /// Gets or sets the property name, such as <c>Tooltip</c> or <c>Info.Transaction</c>.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the formatted value; password text is replaced by a masking placeholder.
        /// </summary>
        public string Value { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents everything the details pane shows for the selected object.
    /// </summary>
    public class ElementSnapshot
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the identifiers from the application down to, but excluding, the selected object.
        /// </summary>
        public IList<string> AncestorIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the readable properties, in catalog order.
        /// </summary>
        public IList<PropertyEntry> Properties { get; set; } = [];

        /// <summary>
        /// Gets or sets the screen rectangle in physical pixels; empty edges mean the object is not visual.
        /// </summary>
        public RectangleModel Rectangle { get; set; } = new();

        /// <summary>
        /// Gets or sets the identifier of the owning session, such as <c>/app/con[0]/ses[0]</c>; empty above session level.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SAP <c>SubType</c> of shells; empty for every other type.
        /// </summary>
        public string SubType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the summary of the selected object.
        /// </summary>
        public ElementSummary Summary { get; set; } = new();

        /// <summary>
        /// Gets or sets the identifier of the owning frame window, such as <c>/app/con[0]/ses[0]/wnd[1]</c>; empty above window level.
        /// </summary>
        public string WindowId { get; set; } = string.Empty;
        #endregion
    }

    /// <summary>
    /// Represents the result of a hover hit test.
    /// </summary>
    public class HitResult
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the absolute identifier of the object under the cursor.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the inner-object description SAP returns for complex controls, such as <c>Cell(2,PRICE)</c>; otherwise empty.
        /// </summary>
        public string InnerObject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the screen rectangle of the object in physical pixels.
        /// </summary>
        public RectangleModel Rectangle { get; set; } = new();

        /// <summary>
        /// Gets or sets the summary of the object under the cursor.
        /// </summary>
        public ElementSummary Summary { get; set; } = new();
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Tests whether another hit targets the same object and inner object.
        /// </summary>
        /// <param name="other">The other hit, or null.</param>
        /// <returns>True when both hits identify the same target.</returns>
        public bool TestSameTarget(HitResult other)
        {
            // Compare identity only; geometry and text can change without the target changing.
            return other is not null
                && string.Equals(Id, other.Id, System.StringComparison.Ordinal)
                && string.Equals(InnerObject, other.InnerObject, System.StringComparison.Ordinal);
        }
        #endregion
    }

    /// <summary>
    /// Represents a cursor sample passed from the UI thread to the hit test.
    /// </summary>
    /// <param name="X">The horizontal screen coordinate in physical pixels.</param>
    /// <param name="Y">The vertical screen coordinate in physical pixels.</param>
    /// <param name="WindowHandle">The top-level window under the cursor, used to find the SAP session.</param>
    public record InspectorPoint(int X, int Y, long WindowHandle);

    /// <summary>
    /// Represents a generated locator and how many objects it matches.
    /// </summary>
    public class LocatorCandidate
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the locator matches exactly the selected object.
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// Gets or sets the number of objects the locator matches in the session.
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Gets or sets the driver locator strategy, such as <c>id</c> or <c>xpath</c>.
        /// </summary>
        public string Strategy { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the object the locator was generated for; used to verify uniqueness.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Gets or sets the locator value.
        /// </summary>
        public string Value { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents a quick action offered for the selected control.
    /// </summary>
    /// <param name="Id">The action identifier, one of the <c>ActionCatalog</c> constants.</param>
    /// <param name="Label">The button label, such as <c>Press</c> or <c>Toggle</c>.</param>
    /// <param name="RequiresText">True when the action uses the action text box.</param>
    public record QuickAction(string Id, string Label, bool RequiresText);

    /// <summary>
    /// Represents a SAP virtual key and the description SAP GUI gives it.
    /// </summary>
    /// <param name="Key">The virtual key code.</param>
    /// <param name="Description">The SAP description, such as <c>Enter</c> or <c>F3</c>.</param>
    public record VirtualKeyEntry(int Key, string Description)
    {
        /// <summary>
        /// Gets the combo box display text.
        /// </summary>
        public string Display => $"{Key} - {Description}";
    }

    /// <summary>
    /// Represents a named, saved set of driver session settings, like an Appium Inspector capability set.
    /// </summary>
    public class CapabilitySet
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the capabilities JSON object placed in <c>alwaysMatch</c>, such as <c>{"sap:options": {...}}</c>.
        /// </summary>
        public string Capabilities { get; set; } = "{}";

        /// <summary>
        /// Gets or sets the driver address.
        /// </summary>
        public string DriverUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name, unique within the saved sets (case-insensitive).
        /// </summary>
        public string Name { get; set; } = string.Empty;
        #endregion
    }

    /// <summary>
    /// Represents a SAP GUI session found on this machine, offered to prefill driver session options.
    /// </summary>
    public class LocalSessionOption
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the SAP client, such as <c>100</c>.
        /// </summary>
        public string Client { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the owning connection's SAP Logon description.
        /// </summary>
        public string ConnectionDescription { get; set; } = string.Empty;

        /// <summary>
        /// Gets the picker text: identifier, system, client, and SAP Logon description.
        /// </summary>
        public string Display => $"{Id}  {SystemName}/{Client}  {ConnectionDescription}".TrimEnd();

        /// <summary>
        /// Gets or sets the session identifier, such as <c>/app/con[0]/ses[0]</c>.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SAP system identifier, such as <c>DEV</c>.
        /// </summary>
        public string SystemName { get; set; } = string.Empty;
        #endregion
    }

    /// <summary>
    /// Describes what an inspection source can do, so the window shows only the features the source supports.
    /// </summary>
    /// <remarks>
    /// Local COM inspection supports everything except the screenshot canvas. A driver session supports what the
    /// driver's W3C and SAP endpoints expose; the desktop overlay works only when the driver runs on this machine,
    /// because only then do the driver's screen coordinates match this desktop.
    /// </remarks>
    public class SourceFeatures
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the source can report the control that has the keyboard focus.
        /// </summary>
        public bool CanFollowFocus { get; init; }

        /// <summary>
        /// Gets a value indicating whether the source can call arbitrary scripting members.
        /// </summary>
        public bool CanInvokeMember { get; init; }

        /// <summary>
        /// Gets a value indicating whether the source can ask SAP GUI to draw its own frame around inner objects.
        /// </summary>
        public bool CanVisualize { get; init; }

        /// <summary>
        /// Gets a value indicating whether hovering over SAP GUI on this desktop resolves controls.
        /// </summary>
        public bool HasDesktopOverlay { get; init; }

        /// <summary>
        /// Gets a value indicating whether the source provides screenshots for the screen canvas.
        /// </summary>
        public bool HasScreenCanvas { get; init; }
        #endregion
    }

    /// <summary>
    /// Represents a screenshot of a SAP window and where that window is on the screen.
    /// </summary>
    public class ScreenCapture
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the PNG image bytes.
        /// </summary>
        public byte[] Image { get; set; } = [];

        /// <summary>
        /// Gets or sets the identifier of the captured window, such as <c>/app/con[0]/ses[0]/wnd[0]</c>.
        /// </summary>
        public string WindowId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the captured window's screen rectangle, used to map image pixels to screen coordinates.
        /// </summary>
        public ElementRectModel WindowRect { get; set; } = new();
        #endregion
    }

    /// <summary>
    /// Represents one generic member invocation from the power-user invoker.
    /// </summary>
    public class MemberInvocation
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies a property read.
        /// </summary>
        public const string GetProperty = "Get property";

        /// <summary>
        /// Specifies a method call.
        /// </summary>
        public const string Method = "Method";

        /// <summary>
        /// Specifies a property write; the first argument is the value.
        /// </summary>
        public const string SetProperty = "Set property";
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the positional arguments, parsed by <c>ArgumentParser</c>.
        /// </summary>
        public object[] Arguments { get; set; } = [];

        /// <summary>
        /// Gets or sets the invocation kind: <see cref="Method"/>, <see cref="GetProperty"/>, or <see cref="SetProperty"/>.
        /// </summary>
        public string Kind { get; set; } = Method;

        /// <summary>
        /// Gets or sets the scripting member name, such as <c>pressToolbarButton</c>.
        /// </summary>
        public string Name { get; set; }
        #endregion
    }
}

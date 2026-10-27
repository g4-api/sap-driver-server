using System;
using System.Collections.Generic;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Lists the simulator's screens and the session information SAP GUI reports for each one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Transactions and programs follow SAP conventions so locators and session information look familiar, but they
    /// are simulator names: <c>FORM</c>, <c>GRID</c>, and the <c>ZSIM_*</c> test lab transactions exist only here.
    /// </para>
    /// <para>
    /// The test lab transactions each collect the elements and automation pitfalls of one area, such as field
    /// attributes or message types. SAP Easy Access lists them in a folder of its own, in catalog order.
    /// </para>
    /// </remarks>
    public static class ScreenCatalog
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the test lab screen with an ALV grid: toolbar, context menus, hotspots, and rows loaded on demand.
        /// </summary>
        public const string Alv = "ZSIM_ALV";

        /// <summary>
        /// Specifies the test lab screen with timing, state, and identity traps: delays, busy periods, optional popups,
        /// duplicates, choice-dependent identifiers, blocking errors, and off-screen controls.
        /// </summary>
        public const string Challenge = "ZSIM_CHALLENGE";

        /// <summary>
        /// Specifies the SAP Easy Access menu shown after logon.
        /// </summary>
        public const string EasyAccess = "SESSION_MANAGER";

        /// <summary>
        /// Specifies the test lab screen with field attributes: read-only, required, maximum length, numeric, padded,
        /// and input help fields.
        /// </summary>
        public const string Fields = "ZSIM_FIELDS";

        /// <summary>
        /// Specifies the form screen with text fields, a check box, radio buttons, a combo box, and tabs.
        /// </summary>
        public const string Form = "FORM";

        /// <summary>
        /// Specifies the grid screen with an ALV-like grid view shell.
        /// </summary>
        public const string Grid = "GRID";

        /// <summary>
        /// Specifies the test lab screen with a classic list: labels by character position, paging, and a secondary list.
        /// </summary>
        public const string List = "ZSIM_LIST";

        /// <summary>
        /// Specifies the logon screen shown when a session opens.
        /// </summary>
        public const string Logon = "S000";

        /// <summary>
        /// Specifies the test lab screen that raises every message type, in the status bar and as popups.
        /// </summary>
        public const string Messages = "ZSIM_MESSAGES";

        /// <summary>
        /// Specifies the test lab screen with shell controls, a box, and a scroll container.
        /// </summary>
        public const string Shells = "ZSIM_SHELLS";

        /// <summary>
        /// Specifies the test lab screen with a table control whose cells exist only for the visible lines.
        /// </summary>
        public const string Table = "ZSIM_TABLE";

        /// <summary>
        /// Specifies the test lab screen with nested tab strips whose subscreen identifiers change between tabs.
        /// </summary>
        public const string Tabs = "ZSIM_TABS";

        /// <summary>
        /// Specifies the test lab screen with a simple, a list, and a column tree with padded keys and lazy folders.
        /// </summary>
        public const string Trees = "ZSIM_TREES";

        // Lists the demo transactions SAP Easy Access shows first, in menu order.
        private static readonly string[] DemoTransactions = [Form, Grid];

        // Holds the information reported for each screen, keyed by transaction.
        private static readonly Dictionary<string, ScreenInfo> Screens = new(StringComparer.Ordinal)
        {
            [Alv] = new ScreenInfo { Program = "SAPMZSIM_ALV", ScreenNumber = 100, Title = "Test Lab: ALV Grid" },
            [Challenge] = new ScreenInfo { Program = "SAPMZSIM_CHALLENGE", ScreenNumber = 300, Title = "Test Lab: Challenge" },
            [EasyAccess] = new ScreenInfo { Program = "SAPLSMTR_NAVIGATION", ScreenNumber = 100, Title = "SAP Easy Access" },
            [Fields] = new ScreenInfo { Program = "SAPMZSIM_FIELDS", ScreenNumber = 100, Title = "Test Lab: Field Attributes" },
            [Form] = new ScreenInfo { Program = "SAPMZSIM_FORM", ScreenNumber = 100, Title = "Simulator Form" },
            [Grid] = new ScreenInfo { Program = "SAPMZSIM_GRID", ScreenNumber = 200, Title = "Simulator Grid" },
            [List] = new ScreenInfo { Program = "SAPMZSIM_LIST", ScreenNumber = 120, Title = "Test Lab: List" },
            [Logon] = new ScreenInfo { Program = "SAPMSYST", ScreenNumber = 20, Title = "SAP" },
            [Messages] = new ScreenInfo { Program = "SAPMZSIM_MESSAGES", ScreenNumber = 100, Title = "Test Lab: Messages and Popups" },
            [Shells] = new ScreenInfo { Program = "SAPMZSIM_SHELLS", ScreenNumber = 100, Title = "Test Lab: Shell Controls" },
            [Table] = new ScreenInfo { Program = "SAPMZSIM_TABLE", ScreenNumber = 100, Title = "Test Lab: Table Control" },
            [Tabs] = new ScreenInfo { Program = "SAPMZSIM_TABS", ScreenNumber = 100, Title = "Test Lab: Tab Strips" },
            [Trees] = new ScreenInfo { Program = "SAPMZSIM_TREES", ScreenNumber = 100, Title = "Test Lab: Trees" }
        };

        // Lists the test lab transactions SAP Easy Access shows in its test lab folder, in menu order.
        private static readonly string[] TestLabTransactions = [Fields, Messages, Tabs, Table, Alv, Trees, Shells, List, Challenge];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the demo transactions, in the order SAP Easy Access lists them.
        /// </summary>
        /// <returns>The transaction codes, such as <c>FORM</c>.</returns>
        public static IReadOnlyList<string> GetDemoTransactions()
        {
            return DemoTransactions;
        }

        /// <summary>
        /// Gets the information reported for a screen.
        /// </summary>
        /// <param name="transaction">The screen's transaction, one of the catalog constants.</param>
        /// <returns>The screen information.</returns>
        /// <exception cref="KeyNotFoundException">The transaction is not a catalog screen.</exception>
        public static ScreenInfo GetScreen(string transaction)
        {
            return Screens[transaction];
        }

        /// <summary>
        /// Gets the test lab transactions, in the order SAP Easy Access lists them.
        /// </summary>
        /// <returns>The transaction codes, such as <c>ZSIM_FIELDS</c>.</returns>
        public static IReadOnlyList<string> GetTestLabTransactions()
        {
            return TestLabTransactions;
        }

        /// <summary>
        /// Tests whether a transaction code names a catalog screen that users can start.
        /// </summary>
        /// <param name="transaction">The transaction code, in upper case and without a <c>/n</c> prefix.</param>
        /// <returns>True for every screen except logon.</returns>
        public static bool TestStartable(string transaction)
        {
            return transaction is not Logon && Screens.ContainsKey(transaction);
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents what SAP GUI reports for one screen in <c>GuiSessionInfo</c> and the window title.
        /// </summary>
        public sealed class ScreenInfo
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets the ABAP program name, such as <c>SAPMSYST</c>.
            /// </summary>
            public string Program { get; init; }

            /// <summary>
            /// Gets the screen (dynpro) number.
            /// </summary>
            public int ScreenNumber { get; init; }

            /// <summary>
            /// Gets the window title.
            /// </summary>
            public string Title { get; init; }
            #endregion
        }
        #endregion
    }
}

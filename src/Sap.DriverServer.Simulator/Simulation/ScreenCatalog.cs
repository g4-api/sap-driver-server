using System;
using System.Collections.Generic;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Lists the simulator's screens and the session information SAP GUI reports for each one.
    /// </summary>
    /// <remarks>
    /// Transactions and programs follow SAP conventions so locators and session information look familiar, but they
    /// are simulator names: <c>FORM</c> and <c>GRID</c> exist only here.
    /// </remarks>
    public static class ScreenCatalog
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the SAP Easy Access menu shown after logon.
        /// </summary>
        public const string EasyAccess = "SESSION_MANAGER";

        /// <summary>
        /// Specifies the form screen with text fields, a check box, radio buttons, a combo box, and tabs.
        /// </summary>
        public const string Form = "FORM";

        /// <summary>
        /// Specifies the grid screen with an ALV-like grid view shell.
        /// </summary>
        public const string Grid = "GRID";

        /// <summary>
        /// Specifies the logon screen shown when a session opens.
        /// </summary>
        public const string Logon = "S000";

        // Holds the information reported for each screen, keyed by transaction.
        private static readonly Dictionary<string, ScreenInfo> Screens = new(StringComparer.Ordinal)
        {
            [EasyAccess] = new ScreenInfo { Program = "SAPLSMTR_NAVIGATION", ScreenNumber = 100, Title = "SAP Easy Access" },
            [Form] = new ScreenInfo { Program = "SAPMZSIM_FORM", ScreenNumber = 100, Title = "Simulator Form" },
            [Grid] = new ScreenInfo { Program = "SAPMZSIM_GRID", ScreenNumber = 200, Title = "Simulator Grid" },
            [Logon] = new ScreenInfo { Program = "SAPMSYST", ScreenNumber = 20, Title = "SAP" }
        };
        #endregion

        #region *** Methods      ***
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

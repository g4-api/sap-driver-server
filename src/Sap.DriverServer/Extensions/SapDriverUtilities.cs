using System;

namespace Sap.DriverServer.Extensions
{
    /// <summary>
    /// Provides start-up console output for the SAP driver server.
    /// </summary>
    public static class SapDriverUtilities
    {
        #region *** Methods      ***
        /// <summary>
        /// Writes the server banner, version, and documentation links to the console.
        /// </summary>
        /// <param name="version">The build version; the release pipeline replaces the <c>0000.00.00.0000</c> placeholder.</param>
        public static void WriteSapAsciiLogo(string version)
        {
            // Write the banner in the same layout as the UIA driver server.
            Console.WriteLine(@"   ____              ____       _                  ____                           ");
            Console.WriteLine(@"  / ___|  __ _ _ __ |  _ \ _ __(_)_   _____ _ __  / ___|  ___ _ ____   _____ _ __ ");
            Console.WriteLine(@"  \___ \ / _` | '_ \| | | | '__| \ \ / / _ \ '__| \___ \ / _ \ '__\ \ / / _ \ '__|");
            Console.WriteLine(@"   ___) | (_| | |_) | |_| | |  | |\ V /  __/ |     ___) |  __/ |   \ V /  __/ |   ");
            Console.WriteLine(@"  |____/ \__,_| .__/|____/|_|  |_| \_/ \___|_|    |____/ \___|_|    \_/ \___|_|   ");
            Console.WriteLine(@"              |_|                                                                 ");
            Console.WriteLine();

            // Write the server identity and links.
            Console.WriteLine("                                   WebDriver Implementation for SAP GUI for Windows");
            Console.WriteLine("                                                  Powered by SAP GUI Scripting API");
            Console.WriteLine();
            Console.WriteLine($"  Version:           {version}");
            Console.WriteLine("  W3C Documentation: https://www.w3.org/TR/webdriver/");
            Console.WriteLine("  Documentation:     https://help.sap.com/docs/sap_gui_for_windows");
            Console.WriteLine("  Open API:          /swagger");
            Console.WriteLine();
        }
        #endregion
    }
}

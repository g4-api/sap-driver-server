namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Provides the capability names understood by the SAP driver server.
    /// </summary>
    public static class SapCapabilities
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the vendor capability that carries the <see cref="SapOptionsModel"/> session selection.
        /// </summary>
        public const string Options = "sap:options";

        /// <summary>
        /// Specifies the capability the server adds to the session response with the attached SAP session details.
        /// </summary>
        public const string Session = "sap:session";
        #endregion
    }
}

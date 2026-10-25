namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the <c>SapDriver</c> section of <c>appsettings.json</c>.
    /// </summary>
    public class SapDriverSettings
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the configuration section name.
        /// </summary>
        public const string SectionName = "SapDriver";
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets how long a request waits for a busy SAP GUI session before failing with
        /// <see cref="SapErrorKinds.SessionBusy"/>.
        /// </summary>
        public int BusyWaitMilliseconds { get; set; } = 10000;

        /// <summary>
        /// Gets or sets how long a request waits for one queued COM operation before failing with
        /// <see cref="SapErrorKinds.Timeout"/>.
        /// </summary>
        /// <remarks>
        /// The time includes queueing behind earlier operations, because every COM call runs on one STA thread.
        /// A timed-out operation keeps running on that thread; its result is discarded when it completes.
        /// </remarks>
        public int ComCallTimeoutMilliseconds { get; set; } = 60000;
        #endregion
    }
}

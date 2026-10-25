namespace Sap.DriverServer.Scripting
{
    /// <summary>
    /// Reads objects that running processes registered in the Windows running object table (ROT).
    /// </summary>
    /// <remarks>
    /// SAP Logon registers its scripting entry point as the <c>SAPGUI</c> item while it runs. Reading the table
    /// directly needs no SAP registration in the registry, so the driver works with a portable or virtualized SAP GUI
    /// and with either process bitness. Windows shows a process only the entries registered by the same user at the
    /// same integrity level. The abstraction exists so tests can attach without a real SAP Logon process.
    /// </remarks>
    public interface IRunningObjectTableReader
    {
        #region *** Methods      ***
        /// <summary>
        /// Gets the object registered under an item name.
        /// </summary>
        /// <param name="itemName">The item name, such as <c>SAPGUI</c>; matched case-insensitively, with or without the <c>!</c> item delimiter.</param>
        /// <returns>The registered object, owned by the caller; null when no entry has that name.</returns>
        object GetObject(string itemName);
        #endregion
    }
}

/*
 * RESOURCES
 * https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-irunningobjecttable-register
 */
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using Sap.DriverServer.Simulator.Native;

namespace Sap.DriverServer.Simulator.Hosting
{
    /// <summary>
    /// Publishes one object in the Windows running object table under an item moniker, the way SAP Logon publishes <c>SAPGUI</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registration uses <c>ROTFLAGS_REGISTRATIONKEEPSALIVE</c>, so the table holds a strong reference until the
    /// registration is disposed. Without <c>ROTFLAGS_ALLOWANYCLIENT</c>, Windows shows the entry only to processes of the
    /// same user at the same integrity level, which is the same rule real SAP Logon is subject to.
    /// </para>
    /// <para>
    /// Register on the thread that owns the published objects: the table marshals the object in the calling apartment,
    /// so client calls arrive on that thread.
    /// </para>
    /// </remarks>
    public sealed class RunningObjectTableRegistration : IDisposable
    {
        #region *** Constants    ***
        // Separates the item name from its delimiter in the moniker display name, as in !SAPGUI.
        private const string ItemDelimiter = "!";

        // Keeps the registered object alive until the registration is revoked (ROTFLAGS_REGISTRATIONKEEPSALIVE).
        private const int KeepAliveFlag = 1;
        #endregion

        #region *** Fields       ***
        // Holds the cookie returned by Register; zero after the registration is revoked.
        private int _cookie;

        // Holds the table the entry was registered in; released on dispose.
        private IRunningObjectTable _table;
        #endregion

        #region *** Constructors ***
        // Wraps an active registration; created only by Register.
        private RunningObjectTableRegistration(IRunningObjectTable table, int cookie)
        {
            // Keep the table and cookie; both are needed to revoke the entry.
            _table = table;
            _cookie = cookie;
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Revoke once; later calls find nothing to release.
            if (_table is null)
            {
                return;
            }

            try
            {
                // Remove the entry so clients see SAP GUI as not running.
                _table.Revoke(_cookie);
            }
            catch (COMException)
            {
                // The entry is already gone, for example after the RPC service cleaned it up.
            }
            finally
            {
                // Release the table reference in every outcome.
                Marshal.ReleaseComObject(_table);
                _table = null;
                _cookie = 0;
            }
        }

        /// <summary>
        /// Registers an object under an item name, refusing when a live entry already uses that name.
        /// </summary>
        /// <param name="itemName">The item name, such as <c>SAPGUI</c>.</param>
        /// <param name="entry">The COM-visible object to publish.</param>
        /// <returns>The registration; dispose it to revoke the entry.</returns>
        /// <exception cref="InvalidOperationException">A live process already registered the name, such as a real SAP Logon.</exception>
        /// <exception cref="COMException">COM refused the registration.</exception>
        public static RunningObjectTableRegistration Register(string itemName, object entry)
        {
            AssertArguments(itemName, entry);

            // Build the same item moniker SAP Logon uses, then look for a live owner of the name.
            var table = NativeMethods.GetRunningObjectTable();
            var moniker = NativeMethods.NewItemMoniker(ItemDelimiter, itemName);

            try
            {
                if (TestLiveEntry(table, moniker))
                {
                    var message = $"Another process already registered '{itemName}' in the running object table. " +
                        "Close SAP Logon or the other simulator first.";
                    throw new InvalidOperationException(message);
                }

                // Publish the entry; the table marshals it in this thread's apartment.
                var cookie = table.Register(KeepAliveFlag, entry, moniker);

                return new RunningObjectTableRegistration(table, cookie);
            }
            catch
            {
                // Release the table on failure; on success the registration owns it.
                Marshal.ReleaseComObject(table);
                throw;
            }
            finally
            {
                // Release the moniker; the table keeps its own copy of the registration.
                Marshal.ReleaseComObject(moniker);
            }

            // Validates the name and the entry before COM is touched.
            static void AssertArguments(string itemName, object entry)
            {
                // Require a name and an entry; both are checked before any COM object is created.
                ArgumentException.ThrowIfNullOrWhiteSpace(argument: itemName);
                ArgumentNullException.ThrowIfNull(argument: entry);
            }
        }

        // Tests whether a registered entry for the moniker belongs to a live process.
        // Entries left by a killed process stay listed until the RPC service cleans them up; binding to one fails, so a
        // failed bind is treated as stale and does not block the registration.
        private static bool TestLiveEntry(IRunningObjectTable table, IMoniker moniker)
        {
            // Skip the bind when the name is not listed at all.
            if (table.IsRunning(moniker) != 0)
            {
                return false;
            }

            // Bind to the listed entry; only a successful bind proves a live owner.
            var result = table.GetObject(moniker, out var existing);
            if (result != 0 || existing is null)
            {
                return false;
            }

            // Release the bound object at once; the check needs no reference to it.
            if (Marshal.IsComObject(existing))
            {
                Marshal.ReleaseComObject(existing);
            }

            return true;
        }
        #endregion
    }
}

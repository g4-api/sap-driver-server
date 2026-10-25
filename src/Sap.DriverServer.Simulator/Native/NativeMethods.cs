/*
 * RESOURCES
 * https://learn.microsoft.com/en-us/windows/win32/api/objbase/nf-objbase-createitemmoniker
 * https://learn.microsoft.com/en-us/windows/win32/api/objbase/nf-objbase-getrunningobjecttable
 */
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Sap.DriverServer.Simulator.Native
{
    /// <summary>
    /// Declares the <c>ole32</c> functions the simulator uses to publish itself in the running object table.
    /// </summary>
    /// <remarks>
    /// The functions return raw interface pointers, which the source-generated marshaller supports. The public wrappers
    /// turn each pointer into a runtime callable wrapper and release the pointer's own reference, so callers own only
    /// the wrapper. Failures are thrown as <see cref="COMException"/>.
    /// </remarks>
    internal static partial class NativeMethods
    {
        #region *** Methods      ***
        /// <summary>
        /// Gets the running object table of the calling user's session.
        /// </summary>
        /// <returns>The table; the caller releases it.</returns>
        /// <exception cref="COMException">COM is not usable in this process.</exception>
        public static IRunningObjectTable GetRunningObjectTable()
        {
            // Read the table pointer and wrap it.
            Marshal.ThrowExceptionForHR(GetRunningObjectTable(reserved: 0, out var pointer));

            return (IRunningObjectTable)ConvertToObject(pointer);
        }

        /// <summary>
        /// Creates an item moniker, such as the <c>!SAPGUI</c> moniker SAP Logon registers.
        /// </summary>
        /// <param name="delimiter">The item delimiter, such as <c>!</c>.</param>
        /// <param name="item">The item name, such as <c>SAPGUI</c>.</param>
        /// <returns>The moniker; the caller releases it.</returns>
        /// <exception cref="COMException">COM failed to create the moniker.</exception>
        public static IMoniker NewItemMoniker(string delimiter, string item)
        {
            // Create the moniker and wrap its pointer.
            Marshal.ThrowExceptionForHR(CreateItemMoniker(delimiter, item, out var pointer));

            return (IMoniker)ConvertToObject(pointer);
        }

        // Wraps an interface pointer in a runtime callable wrapper and releases the pointer's reference.
        private static object ConvertToObject(IntPtr pointer)
        {
            try
            {
                // Wrap the pointer; the wrapper takes its own reference.
                return Marshal.GetObjectForIUnknown(pointer);
            }
            finally
            {
                // Release the reference the native call returned, so only the wrapper keeps the object alive.
                Marshal.Release(pointer);
            }
        }

        // Creates an item moniker and returns its IMoniker pointer.
        [LibraryImport("ole32.dll", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int CreateItemMoniker(string delimiter, string item, out IntPtr moniker);

        // Returns the IRunningObjectTable pointer of the calling user's session.
        [LibraryImport("ole32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial int GetRunningObjectTable(int reserved, out IntPtr table);
        #endregion
    }
}

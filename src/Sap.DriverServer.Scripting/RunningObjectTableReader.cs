/*
 * RESOURCES
 * https://learn.microsoft.com/en-us/windows/win32/api/objidl/nn-objidl-irunningobjecttable
 */
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using Sap.DriverServer.Domain;

namespace Sap.DriverServer.Scripting
{
    /// <summary>
    /// Default <see cref="IRunningObjectTableReader"/> that enumerates the running object table through <c>ole32</c>.
    /// </summary>
    /// <remarks>
    /// The reader replaces the <c>SapROTWr.SapROTWrapper</c> component, which exists only because script hosts cannot
    /// call <c>GetRunningObjectTable</c>. Each call enumerates the table, compares moniker display names, and returns
    /// the first match. Every moniker, enumerator, bind context, and table reference is released before the call
    /// returns; only the returned object is left to the caller. Entries whose display name cannot be read are skipped.
    /// Call it on the thread that owns the scripting apartment.
    /// </remarks>
    public sealed partial class RunningObjectTableReader : IRunningObjectTableReader
    {
        #region *** Constants    ***
        // Separates the item name from the delimiter in an item moniker display name, such as !SAPGUI.
        private const char ItemDelimiter = '!';
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public object GetObject(string itemName)
        {
            // Require a name; an empty name matches no entry.
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: itemName);

            // Open the table and a bind context; both calls fail only when COM is unusable in this process.
            Marshal.ThrowExceptionForHR(NativeMethods.GetRunningObjectTable(reserved: 0, out var table));
            IBindCtx bindContext = null;
            IEnumMoniker enumerator = null;

            try
            {
                Marshal.ThrowExceptionForHR(NativeMethods.CreateBindCtx(reserved: 0, out bindContext));
                table.EnumRunning(out enumerator);

                // Walk the entries one at a time and return the first one whose display name matches.
                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var entry = GetMatchingObject(table, bindContext, monikers[0], itemName);
                    if (entry is not null)
                    {
                        return entry;
                    }
                }

                return null;
            }
            finally
            {
                // Release the enumeration objects; the returned entry holds its own reference.
                ReleaseComObject(enumerator);
                ReleaseComObject(bindContext);
                ReleaseComObject(table);
            }
        }

        // Returns the object behind one moniker when its display name matches the item name; otherwise null.
        // The moniker is always released; unreadable entries, such as ones from exiting processes, are skipped.
        private static object GetMatchingObject(IRunningObjectTable table, IBindCtx bindContext, IMoniker moniker, string itemName)
        {
            try
            {
                // Compare the display name without the item delimiter, so !SAPGUI and SAPGUI both match.
                moniker.GetDisplayName(bindContext, pmkToLeft: null, out var displayName);
                if (!string.Equals(displayName?.TrimStart(ItemDelimiter), itemName.TrimStart(ItemDelimiter), StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                // Bind to the running object; a failure here means the entry exists but is not reachable.
                Marshal.ThrowExceptionForHR(table.GetObject(moniker, out var entry));

                return entry;
            }
            catch (COMException exception) when (exception.HResult != SapErrorMapper.AccessDenied)
            {
                // Skip entries that vanished or refuse a display name; access denial propagates for classification.
                return null;
            }
            finally
            {
                // Release the moniker; the table keeps its own registration.
                ReleaseComObject(moniker);
            }
        }

        // Releases one RCW reference, ignoring null values.
        private static void ReleaseComObject(object comObject)
        {
            // Skip values that hold no COM reference.
            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        #endregion

        #region *** Nested Types ***
        // Declares the ole32 entry points that expose the running object table to managed code.
        // They return raw interface pointers, which the source-generated marshaller supports; the wrappers below turn
        // each pointer into a runtime callable wrapper and release the pointer's own reference.
        private static partial class NativeMethods
        {
            // Creates a bind context, which IMoniker.GetDisplayName requires.
            internal static int CreateBindCtx(int reserved, out IBindCtx bindContext)
            {
                // Create the bind context, then wrap its pointer only on success.
                var result = CreateBindCtxNative(reserved, out var pointer);
                bindContext = result == 0 ? (IBindCtx)ConvertToObject(pointer) : null;

                return result;
            }

            // Returns the running object table of the calling user's session.
            internal static int GetRunningObjectTable(int reserved, out IRunningObjectTable table)
            {
                // Read the table pointer, then wrap it only on success.
                var result = GetRunningObjectTableNative(reserved, out var pointer);
                table = result == 0 ? (IRunningObjectTable)ConvertToObject(pointer) : null;

                return result;
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

            // Creates a bind context and returns its IBindCtx pointer.
            [LibraryImport("ole32.dll", EntryPoint = "CreateBindCtx")]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            private static partial int CreateBindCtxNative(int reserved, out IntPtr bindContext);

            // Returns the IRunningObjectTable pointer of the calling user's session.
            [LibraryImport("ole32.dll", EntryPoint = "GetRunningObjectTable")]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            private static partial int GetRunningObjectTableNative(int reserved, out IntPtr table);
        }
        #endregion
    }
}

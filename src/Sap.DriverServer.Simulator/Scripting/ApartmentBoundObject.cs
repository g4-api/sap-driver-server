/*
 * RESOURCES
 * https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.icustomqueryinterface
 */
using System;
using System.Runtime.InteropServices;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Base class of every scripting object: binds the object to the apartment of the thread that created it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// .NET makes its COM wrappers agile: they aggregate the free-threaded marshaler, so calls from other processes
    /// arrive on RPC worker threads. SAP GUI is single-threaded, and the simulator's objects touch WPF elements, so
    /// every call has to run on the UI thread. Refusing <c>IMarshal</c> and <c>IAgileObject</c> makes COM use standard
    /// marshaling instead, which delivers every call through the UI thread's message loop, as for SAP GUI.
    /// </para>
    /// <para>
    /// Members are exposed through the default <c>AutoDispatch</c> class interface, so clients call them by name through
    /// <c>IDispatch</c>; names are matched case-insensitively, which lets SAP's spelling, such as <c>findById</c>, work.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public abstract class ApartmentBoundObject : ICustomQueryInterface
    {
        #region *** Constants    ***
        // Identifies IAgileObject, which marks an object as callable from any apartment.
        private static readonly Guid AgileObjectInterfaceId = new("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");

        // Identifies IMarshal, through which the free-threaded marshaler bypasses apartment rules.
        private static readonly Guid MarshalInterfaceId = new("00000003-0000-0000-C000-000000000046");
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out IntPtr ppv)
        {
            // Refuse the two interfaces that make a wrapper agile; let the runtime answer every other request.
            ppv = IntPtr.Zero;
            var isAgileInterface = iid == MarshalInterfaceId || iid == AgileObjectInterfaceId;

            return isAgileInterface ? CustomQueryInterfaceResult.Failed : CustomQueryInterfaceResult.NotHandled;
        }
        #endregion
    }
}
